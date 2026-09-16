using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoLock;
using ChronoStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Separate batch process only. Invokes real uGUI callbacks; does not synthesize hardware clicks.
[InitializeOnLoad]
public static class StationMenuCheck
{
    const string Key = "Station.MenuQA.";
    static StationGame game;
    static int phase, frame = -1, waitFrames;
    static float openingTime;
    static double began;
    static readonly List<string> checks = new List<string>();
    static string Output => SessionState.GetString(Key + "Output", "Builds/StationMenuQA");
    static StationUI UI => game.GetComponent<StationUI>();
    static StationHandView Hand => game.Player.View.GetComponent<StationHandView>();
    static StationMenuCheck() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("StationMenuCheck requires a separate batch Unity process; do not pass -quit.");
        string output = Path.GetFullPath("Builds/StationMenuQA"); var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--chrono-menu-out") output = Path.GetFullPath(args[i + 1]);
        SessionState.SetString(Key + "Output", output); Directory.CreateDirectory(output);
        SessionState.SetBool(Key + "HadKey", PlayerPrefs.HasKey(ChronoPreferences.StorageKey));
        SessionState.SetString(Key + "Raw", PlayerPrefs.GetString(ChronoPreferences.StorageKey, ""));
        SessionState.SetString(Key + "Runtime", JsonUtility.ToJson(ChronoPreferences.Current));
        SessionState.SetBool(Key + "Running", true);
        try
        {
            var temporalErrors = StationTemporalTests.Run();
            Check(temporalErrors.Count == 0, "Temporal regression including thirty-second mid-motion stop: " + string.Join("; ", temporalErrors));
            var go = new GameObject("Temporary cargo history check");
            try
            {
                var cargo = go.AddComponent<StationTemporalObject>(); cargo.kind = StationDeviceKind.Cargo;
                cargo.endpointB = Vector3.right * 5; cargo.baseRate = .16f; cargo.Initialize();
                cargo.Apply(StationAbility.Slow); cargo.Tick(35);
                Check(!cargo.HasInitialHistory, "Cargo movement beyond twelve recorded seconds reports lost initial history");
                cargo.ResetDevice(); Check(cargo.HasInitialHistory, "Cargo reset restores actual initial history");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            SessionState.SetString(Key + "Preflight", string.Join("\n", checks));
            EditorSceneManager.OpenScene("Assets/ChronoStation/Scenes/ChronoStationDemo.unity");
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks.Add(message); Debug.Log("STATION_MENU_PASS " + message); }
    static void Click(string label)
    {
        foreach (var button in UI.Canvas.GetComponentsInChildren<Button>())
            if (button.interactable && button.GetComponentInChildren<Text>().text == label) { button.onClick.Invoke(); return; }
        throw new Exception("Visible production button missing: " + label);
    }
    static Slider Fov()
    {
        foreach (var slider in UI.Canvas.GetComponentsInChildren<Slider>()) if (slider.name == "시야각 slider") return slider;
        throw new Exception("FOV slider missing");
    }
    static void Next() { phase++; waitFrames = 0; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key + "Running", false)) return;
        try
        {
            if (began == 0) began = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - began > 60) throw new Exception("Menu QA timed out at phase " + phase);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!game)
            {
                game = UnityEngine.Object.FindFirstObjectByType<StationGame>();
                if (!game || !game.GetComponent<StationUI>() || !Hand || !Hand.Presentation) { game = null; return; }
                game.Player.AutomatedInput = true; waitFrames = 0;
            }
            if (++waitFrames < 5) return;
            switch (phase)
            {
                case 0:
                    Check(game.Page == StationPage.Title && !Hand.IsVisible, "Title hides first-person hand");
                    Click("설정"); Next(); break;
                case 1:
                    Check(game.Page == StationPage.Options, "Title settings callback opens options");
                    float old = ChronoPreferences.Current.fov; SessionState.SetFloat(Key + "OldFov", old);
                    Fov().value = old < 80 ? 96 : 65;
                    Check(ChronoPreferences.Current.fov == old, "Slider draft does not change live preferences");
                    foreach (var slider in UI.Canvas.GetComponentsInChildren<Slider>())
                        Check(slider.handleRect.rect.height > 10 && slider.handleRect.rect.height <= 42, "Laid-out slider handle remains inside its row: " + slider.name);
                    Capture("01-options-1280x720.png", 720); Click("돌아가기"); Next(); break;
                case 2:
                    Check(game.Page == StationPage.Title && ChronoPreferences.Current.fov == SessionState.GetFloat(Key + "OldFov", 78), "Back cancels options draft");
                    Check(PlayerPrefs.HasKey(ChronoPreferences.StorageKey) == SessionState.GetBool(Key + "HadKey", false)
                        && PlayerPrefs.GetString(ChronoPreferences.StorageKey, "") == SessionState.GetString(Key + "Raw", ""), "Canceled draft leaves stored preferences unchanged");
                    Click("설정"); Next(); break;
                case 3:
                    Check(Fov().value == ChronoPreferences.Current.fov, "Reopened options discards previous draft");
                    Fov().value = 90; Click("적용 및 저장");
                    Check(ChronoPreferences.Current.fov == 90, "Apply callback commits FOV to current settings");
                    Check(JsonUtility.FromJson<ChronoSettingsData>(PlayerPrefs.GetString(ChronoPreferences.StorageKey)).fov == 90, "Apply callback persists FOV");
                    Capture("02-options-1280x800.png", 800); Click("돌아가기"); Next(); break;
                case 4:
                    Click("새 게임 시작"); Check(game.Opening && !game.AcceptsInput, "New-game UI callback guards the same input frame"); Next(); break;
                case 5:
                    Check(game.Opening && game.OpeningElapsed > 0, "New game starts actual awakening");
                    openingTime = game.OpeningElapsed; game.Pause(); Next(); break;
                case 6:
                    Check(game.Page == StationPage.Pause && game.OpeningElapsed == openingTime && !Hand.IsVisible, "Pause freezes awakening clock and hides hand");
                    Click("계속하기"); Check(!game.AcceptsInput && !game.SelfActive.HasValue, "Resume callback blocks immediate gameplay input without casting"); Next(); break;
                case 7:
                    Check(game.OpeningElapsed > openingTime && !game.SelfActive.HasValue, "Awakening resumes without stray ability");
                    game.SkipOpening(); Next(); break;
                case 8:
                    Check(game.IsPlaying && Hand.IsVisible, "Standing HUD shows first-person hand"); Capture("03-hud-1280x800.png", 800);
                    game.Pause(); Next(); break;
                case 9:
                    Click("계속하기"); Check(!game.AcceptsInput && !game.SelfActive.HasValue, "Standing resume button also guards immediate input"); Next(); break;
                case 10:
                    Check(game.AcceptsInput && !game.SelfActive.HasValue && Hand.IsVisible, "Gameplay input unlocks after menu guard with no unwanted cast");
                    Finish(true, "STATION_MENU_QA_PASSED"); break;
            }
        }
        catch (Exception e) { Finish(false, e + "\nPhase=" + phase); }
    }
    static void Capture(string name, int height)
    {
        var camera = game.Player.View; var canvas = UI.Canvas; var rt = new RenderTexture(1280, height, 24);
        var target = camera.targetTexture; var active = RenderTexture.active; var mode = canvas.renderMode;
        var previousCamera = canvas.worldCamera; float plane = canvas.planeDistance, aspect = camera.aspect; bool hdr = camera.allowHDR;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = rt; camera.aspect = 1280f / height; camera.allowHDR = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .1f;
            Canvas.ForceUpdateCanvases();
            typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(canvas.GetComponent<CanvasScaler>(), null);
            Canvas.ForceUpdateCanvases(); typeof(StationUI).GetMethod("Fit", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(UI, null); Canvas.ForceUpdateCanvases();
            var content = canvas.transform.Find("Page " + game.Page) as RectTransform; var corners = new Vector3[4]; content.GetWorldCorners(corners);
            foreach (var corner in corners) { var p = camera.WorldToViewportPoint(corner); Check(p.x >= -.002f && p.x <= 1.002f && p.y >= -.002f && p.y <= 1.002f, "Page bounds fit capture: " + name); }
            camera.Render(); RenderTexture.active = rt; pixels = new Texture2D(1280, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, height), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Output, name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; camera.aspect = aspect; camera.allowHDR = hdr;
            canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane; RenderTexture.active = active;
            if (pixels) UnityEngine.Object.DestroyImmediate(pixels); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Running", false) || stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Finish(bool success, string message)
    {
        if (!SessionState.GetBool(Key + "Running", false)) return; SessionState.SetBool(Key + "Running", false);
        try
        {
            if (SessionState.GetBool(Key + "HadKey", false)) PlayerPrefs.SetString(ChronoPreferences.StorageKey, SessionState.GetString(Key + "Raw", ""));
            else PlayerPrefs.DeleteKey(ChronoPreferences.StorageKey);
            PlayerPrefs.Save(); ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(SessionState.GetString(Key + "Runtime", "{}")));
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "menu-result.txt"), message + "\n" + SessionState.GetString(Key + "Preflight", "") + "\n" + string.Join("\n", checks) + "\nProduction uGUI callbacks and public gameplay APIs; not hardware clicks. Camera-space SDR captures may differ from normal overlay composition. Original preference key and runtime settings restored.");
        }
        finally { EditorApplication.Exit(success ? 0 : 1); }
    }
}

