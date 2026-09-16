using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoStation;
using ChronoLock;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class StationDemoCheck
{
    const string Running = "Station.DemoQA", Settings = "Station.QA.Settings";
    static readonly List<string> checks = new List<string>();
    static StationGame game;
    static int phase, frame = -1, stageAttempts;
    static double began, at;
    static float remembered, oldElapsed;
    static Vector3 held;
    static string output => Path.GetFullPath("Builds/StationQA");
    static StationDemoCheck() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Run QA in a separate batch Unity process.");
        Directory.CreateDirectory(output);
        var errors = StationTemporalTests.Run();
        File.WriteAllText(Path.Combine(output, "temporal-result.txt"), errors.Count == 0 ? "PASS: temporal component tests" : string.Join("\n", errors));
        if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
        EditorSceneManager.OpenScene("Assets/ChronoStation/Scenes/ChronoStationDemo.unity");
        SessionState.SetString(Settings, JsonUtility.ToJson(ChronoPreferences.Current));
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Running, false) || stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); Debug.Log("STATION_PASS " + message); }
    static void Next() { phase++; at = EditorApplication.timeSinceStartup; Debug.Log("STATION_QA_PHASE " + phase); }
    static double Age => EditorApplication.timeSinceStartup - at;
    static Vector3 Local => game.CurrentStage.Local(game.Player.transform.position);
    static void Move(float x, float z)
    {
        var goal = game.CurrentStage.transform.TransformPoint(new Vector3(x, 0, z));
        var delta = goal - game.Player.transform.position; delta.y = 0;
        var dir = game.Player.transform.InverseTransformDirection(delta.normalized);
        game.Player.TestMove = delta.magnitude < .12f ? Vector2.zero : new Vector2(dir.x, dir.z);
    }
    static void Stop() { game.Player.TestMove = Vector2.zero; }
    static void FaceDevice()
    {
        var device = game.CurrentStage.device;
        // Aiming at the device's authored control collider uses the same physics ray as the player.
        var colliders = device.GetComponentsInChildren<Collider>();
        Collider best = null;
        float distance = float.MaxValue;
        foreach (var collider in colliders)
        {
            if (collider.transform == device.movingPart || collider.transform.IsChildOf(device.movingPart)) continue;
            float d = Vector3.Distance(collider.bounds.center, game.Player.View.transform.position);
            if (d < distance) { best = collider; distance = d; }
        }
        game.Player.LookAt(best ? best.bounds.center : device.movingPart.position);
    }
    static void Exit()
    {
        var p = game.CurrentStage.Local(game.CurrentStage.exit.position);
        Move(p.x, p.z);
        if (Vector2.Distance(new Vector2(Local.x, Local.z), new Vector2(p.x, p.z)) < 1) { Stop(); game.Interact(); }
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Running, false)) return;
        try
        {
            if (began == 0) began = at = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - began > 200) throw new Exception("Timed out phase " + phase + " local " + (game ? Local.ToString() : "no game"));
            if (game && Age > 22) throw new Exception("Phase timeout " + phase + " local " + Local + " device " + (game.CurrentStage.device ? game.CurrentStage.device.Progress.ToString("0.00") : "none"));
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!game)
            {
                game = UnityEngine.Object.FindFirstObjectByType<StationGame>();
                if (!game || !game.GetComponent<StationUI>() || !game.Player.View.GetComponent<StationHandView>().Presentation) { game = null; return; }
                game.Player.AutomatedInput = true;
                var prefs = ChronoPreferences.Current; prefs.cameraMotion = false; prefs.fov = 78; ChronoPreferences.Apply(prefs);
                at = EditorApplication.timeSinceStartup; return;
            }
            if (Age < .1) return;
            switch (phase)
            {
                case 0:
                    Check(game.Page == StationPage.Title, "Boot opens revised title"); Capture("01-title.png");
                    game.NewGame(); Next(); break;
                case 1:
                    if (game.OpeningElapsed < 3.8f) break;
                    Capture("02-awakening.png"); remembered = game.OpeningElapsed; game.Pause(); Next(); break;
                case 2:
                    if (Age < .4) break;
                    Check(game.OpeningElapsed == remembered, "Pause freezes awakening"); game.BackToTitle(); game.Resume();
                    Check(!game.Opening && game.Player.View.transform.localPosition.y > 1.6f, "Title-return during awakening safely restores standing view");
                    Check(!game.UseObjectAbility() && !game.ObjectUnlocked, "Object input locked at start without affecting self");
                    game.SelectAbility(StationAbility.Rewind); Check(game.Selected == StationAbility.Accelerate, "Later self abilities stay locked");
                    stageAttempts = game.Attempts; game.Interact(); Move(0, 26); Next(); break;
                case 3:
                    if (game.Attempts == stageAttempts) break;
                    Stop(); Check(!game.StageSolved && Local.z < 4, "Normal speed cannot pass timed door; failure resets current stage");
                    Check(game.UseSelfAbility(), "Self acceleration cast accepted"); game.Interact(); Move(0, 26); Next(); break;
                case 4:
                    if (!game.StageSolved) break;
                    Check(Local.z > 21, "Accelerated player physically crosses timed gate"); Capture("03-self-accelerate.png"); Next(); break;
                case 5:
                    if (game.Step == 0) { Exit(); break; }
                    Check(game.Step == 1 && game.UnlockedAbilities == 2, "Second self ability unlocks in stage two"); Move(0, 5.3f); Next(); break;
                case 6:
                    if (Local.z < 5.1f) break;
                    Stop(); game.Player.TestJump = true; Move(0, 19); stageAttempts = game.Attempts; Next(); break;
                case 7:
                    if (game.Attempts == stageAttempts) break;
                    Check(game.Step == 1 && !game.StageSolved, "Ordinary jump cannot bypass slow-air challenge"); Move(0, 5.3f); Next(); break;
                case 8:
                    if (Local.z < 5.1f) break;
                    game.SelectAbility(StationAbility.Slow); Check(game.UseSelfAbility(), "Self slowdown activated");
                    game.Player.TestJump = true; Move(0, 18); Next(); break;
                case 9:
                    if (!game.StageSolved) break;
                    Check(Local.z > 16 && game.Attempts == stageAttempts + 1, "Slow-air jump crosses eight-metre gap without teleportation");
                    Capture("04-self-slow.png"); game.ReleaseAbility(); Next(); break;
                case 10:
                    if (game.Step == 1) { Exit(); break; }
                    Move(0, 7.9f); Next(); break;
                case 11:
                    if (Local.z < 7.5f) break;
                    Stop(); game.Interact(); Next(); break;
                case 12:
                    if (game.SequenceTime < 2.25f) break;
                    game.SelectAbility(StationAbility.Stop); Check(game.UseSelfAbility(), "Self stop activated at impact warning");
                    held = game.Player.transform.position; Move(0, 10); Next(); break;
                case 13:
                    if (!game.StageSolved) break;
                    Check(Vector3.Distance(held, game.Player.transform.position) < .02f, "Self stop roots player and blocks scanner impact");
                    Capture("05-self-stop.png"); Stop(); game.ReleaseAbility(); Next(); break;
                case 14:
                    if (game.Step == 2) { Exit(); break; }
                    Move(0, 22); Next(); break;
                case 15:
                    if (Local.z < 21.5f) break;
                    Stop(); game.Interact(); Check(game.MemoryFound, "Observation record obtained through E interaction");
                    game.SelectAbility(StationAbility.Rewind); Check(game.UseSelfAbility(), "Self rewind activated"); Next(); break;
                case 16:
                    if (game.Rewinding) break;
                    Check(game.Step == 3 && Local.z < 3 && game.StageSolved && game.MemoryFound, "Self rewind restores current entrance and preserves acquired memory");
                    Capture("06-self-rewind.png"); Next(); break;
                case 17:
                    if (game.Step == 3) { Exit(); break; }
                    Check(game.ObjectUnlocked && game.UnlockedAbilities == 4, "All four self lessons precede object unlock");
                    Move(0, 5); Next(); break;
                case 18:
                    if (Local.z < 4.8f) break;
                    Stop(); FaceDevice(); Next(); break;
                case 19:
                    Check(game.Focused == game.CurrentStage.device, "Real aim ray identifies generator");
                    game.SelectAbility(StationAbility.Accelerate); Check(game.UseObjectAbility(), "Object acceleration accepted");
                    remembered = game.CurrentStage.device.Progress; oldElapsed = game.Elapsed; game.Pause(); Capture("07-pause.png"); Next(); break;
                case 20:
                    if (Age < .6f) break;
                    Check(game.CurrentStage.device.Progress == remembered && game.Elapsed == oldElapsed, "Pause freezes object timeline and run clock");
                    game.ShowOptions(); Capture("08-options.png"); game.Back(); game.Resume(); Next(); break;
                case 21:
                    if (!game.StageSolved) break;
                    Check(game.CurrentStage.device.Progress >= .99f, "Generator acceleration supplies full power"); Capture("09-generator.png"); Next(); break;
                case 22:
                    if (game.Step == 4) { Exit(); break; }
                    Move(0, 4.8f); Next(); break;
                case 23:
                    if (Local.z < 4.5f) break;
                    Stop(); FaceDevice(); Next(); break;
                case 24:
                    if (game.CurrentStage.device.Progress > .1f) break;
                    Check(game.Focused == game.CurrentStage.device, "Real ray identifies platform control");
                    game.SelectAbility(StationAbility.Slow); Check(game.UseObjectAbility(), "Moving platform slowed");
                    Move(0, 7.6f); Next(); break;
                case 25:
                    if (Local.z < 7.2f) break;
                    Stop(); game.ReleaseAbility(); Next(); break;
                case 26:
                    // The player boarded near the trailing edge, not the platform centre.
                    if (Local.z < 15.35f || game.CurrentStage.device.Progress < .92f) break;
                    Check(game.Player.Grounded, "CharacterController is carried by moving platform"); game.Player.TestJump = true; Move(0, 23); Next(); break;
                case 27:
                    if (!game.StageSolved) break;
                    Capture("10-platform.png"); Next(); break;
                case 28:
                    if (game.Step == 5) { Exit(); break; }
                    Move(0, 7); Next(); break;
                case 29:
                    if (Local.z < 6.8f) break;
                    Stop(); FaceDevice(); Next(); break;
                case 30:
                    Check(game.Focused == game.CurrentStage.device, "Real ray identifies rotor control"); game.SelectAbility(StationAbility.Stop);
                    Check(game.UseObjectAbility(), "Object stop accepted"); remembered = game.CurrentStage.device.Progress; Move(0, 23); Next(); break;
                case 31:
                    if (!game.StageSolved) break;
                    Check(game.CurrentStage.device.Progress == remembered && Local.z > 13, "Stopped rotor remains fixed during safe passage"); Capture("11-rotor.png"); Next(); break;
                case 32:
                    if (game.Step == 6) { Exit(); break; }
                    Move(0, 4.8f); Next(); break;
                case 33:
                    if (Local.z < 4.5f) break;
                    Stop(); FaceDevice(); Next(); break;
                case 34:
                    if (game.CurrentStage.device.Progress < .99f) break;
                    Check(game.CurrentStage.device.HistoryCount > 100, "Final bridge records its live displacement"); Capture("12-cargo-displaced.png");
                    Check(game.Focused == game.CurrentStage.device, "Real ray identifies cargo control");
                    game.SelectAbility(StationAbility.Rewind); Check(game.UseObjectAbility(), "Object rewind accepted with actual history"); Next(); break;
                case 35:
                    if (!game.StageSolved) break;
                    Check(game.CurrentStage.device.Secured && game.CurrentStage.device.Progress <= .015f, "Actual cargo history restores and secures bridge");
                    Capture("13-cargo-restored.png"); Next(); break;
                case 36:
                    if (game.Page != StationPage.Completed) { Exit(); break; }
                    Check(!game.HasStarted, "Complete run reaches return-flight ending"); Capture("14-completed.png"); Finish(true, "STATION_DEMO_QA_PASSED"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex + "\nphase=" + phase + (game ? " local=" + Local + " step=" + game.Step : "")); }
    }
    static void Capture(string name)
    {
        var camera = game.Player.View; var canvas = game.GetComponent<StationUI>().Canvas;
        var rt = new RenderTexture(1280, 720, 24); var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var mode = canvas.renderMode; var uiCamera = canvas.worldCamera; float plane = canvas.planeDistance, aspect = camera.aspect;
        bool hdr = camera.allowHDR;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = rt; camera.aspect = 1280f / 720; camera.allowHDR = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .1f;
            Canvas.ForceUpdateCanvases();
            var scaler = canvas.GetComponent<CanvasScaler>(); typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(scaler, null);
            Canvas.ForceUpdateCanvases();
            typeof(StationUI).GetMethod("Fit", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(game.GetComponent<StationUI>(), null);
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = rt; pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; camera.aspect = aspect; camera.allowHDR = hdr;
            canvas.renderMode = mode; canvas.worldCamera = uiCamera; canvas.planeDistance = plane;
            RenderTexture.active = active; if (pixels) UnityEngine.Object.DestroyImmediate(pixels); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    static void Finish(bool success, string message)
    {
        if (!SessionState.GetBool(Running, false)) return;
        SessionState.SetBool(Running, false);
        try
        {
            ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(SessionState.GetString(Settings, "{}")));
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "demo-result.txt"), message + "\n" + string.Join("\n", checks) + "\nAutomated input uses real CharacterController, physics targeting, and gameplay methods; no stage teleports or forced solved flags. Captures use temporary camera-space HUD and SDR, so overlay-hand layering differs from normal UI rendering. Human playtime not measured.");
        }
        finally { EditorApplication.Exit(success ? 0 : 1); }
    }
}

