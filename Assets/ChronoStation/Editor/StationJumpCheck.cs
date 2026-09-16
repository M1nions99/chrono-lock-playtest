using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoStation;
using ChronoLock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated trajectory regression on the real controller and room colliders.
[InitializeOnLoad]
public static class StationJumpCheck
{
    const string Running = "Station.JumpQA";
    static readonly int[] rates = { 30, 60, 120 };
    static readonly string[] modes = { "normal", "slow before jump", "slow during ascent", "release during ascent", "expire during ascent" };
    static readonly List<string> results = new List<string>();
    static StationGame game;
    static int rate, mode, frame = -1, settle;
    static bool initialized, jumping, departed, toggled;
    static float baseY, peak, takeoff, baseline, normalDuration;
    static double began;
    static string Output => Path.GetFullPath("Builds/StationJumpQA/jump-result.txt");
    static StationJumpCheck() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Use a separate batch Unity process.");
        EditorSceneManager.OpenScene("Assets/ChronoStation/Scenes/ChronoStationDemo.unity");
        SessionState.SetString(Running + ".prefs", JsonUtility.ToJson(ChronoPreferences.Current));
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    static void BeginCase()
    {
        Time.captureDeltaTime = 1f / rates[rate];
        game.Retry(); game.Player.TestMove = Vector2.zero; game.SelectAbility(StationAbility.Slow);
        initialized = jumping = departed = toggled = false; settle = 0; peak = 0;
        Debug.Log("JUMP_CASE " + rates[rate] + "Hz " + modes[mode]);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Running, false)) return;
        try
        {
            if (began == 0) began = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - began > 100) throw new Exception("Jump regression timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!game)
            {
                game = UnityEngine.Object.FindFirstObjectByType<StationGame>();
                if (!game || !game.GetComponent<StationUI>()) { game = null; return; }
                game.Player.AutomatedInput = true; game.NewGame(); game.SkipOpening();
                // Fixture setup selects the existing slow lesson; completion flags are never set.
                typeof(StationGame).GetMethod("SelectStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 1 });
                BeginCase(); return;
            }
            if (++settle < 6 || !game.AcceptsInput) return;
            if (!initialized)
            {
                if (!game.Player.Grounded) return;
                if (mode == 1 || mode == 3 || mode == 4) Need(game.UseSelfAbility(), "Can activate slow for fixture");
                initialized = true;
            }
            if (!jumping)
            {
                if (mode == 4 && game.SelfRemaining > .7f) return;
                baseY = game.Player.transform.position.y; takeoff = game.Elapsed;
                game.Player.TestJump = true; jumping = true; return;
            }
            float height = game.Player.transform.position.y - baseY;
            peak = Mathf.Max(peak, height);
            if (height > .08f) departed = true;
            if (!toggled && height > .36f)
            {
                if (mode == 2) { Need(game.UseSelfAbility(), "Can activate slow while rising"); toggled = true; }
                if (mode == 3) { game.ReleaseAbility(); toggled = true; }
            }
            if (!departed || !game.Player.Grounded) return;
            float duration = game.Elapsed - takeoff;
            Need(peak > .85f && peak < 1.05f, "Jump stays near the intended 0.96m height");
            if (mode == 0) { baseline = peak; normalDuration = duration; }
            else Need(Mathf.Abs(peak - baseline) < .035f, "Slow toggles and expiry must preserve normal apex");
            if (mode == 1) Need(duration > normalDuration * 4.5f && duration < normalDuration * 5.5f, "Slow jump stretches airtime by five without adding height");
            if (mode == 2 || mode == 3) Need(toggled, "Midair transition happened before apex");
            if (mode == 4) Need(!game.SelfActive.HasValue, "Slow expired during jump");
            results.Add(rates[rate] + "Hz | " + modes[mode] + " | apex=" + peak.ToString("0.000") + "m | airtime=" + duration.ToString("0.000") + "s");
            mode++;
            if (mode == modes.Length) { mode = 0; rate++; }
            if (rate == rates.Length) { Finish(true, "STATION_JUMP_QA_PASSED"); return; }
            BeginCase();
        }
        catch (Exception e) { Finish(false, e + "\nrate=" + rate + " mode=" + mode + " peak=" + peak); }
    }
    static void Need(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Running, false) || stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Finish(bool ok, string message)
    {
        if (!SessionState.GetBool(Running, false)) return;
        SessionState.SetBool(Running, false); Time.captureDeltaTime = 0;
        ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(SessionState.GetString(Running + ".prefs", "{}")));
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, message + "\n" + string.Join("\n", results) + "\nReal controller on authored flat floor. Deterministic 30/60/120 Hz simulation steps; not a claim of measured rendering frame rate. Existing stage selected as isolated test setup. No transform injection during flight.");
        EditorApplication.Exit(ok ? 0 : 1);
    }
}

