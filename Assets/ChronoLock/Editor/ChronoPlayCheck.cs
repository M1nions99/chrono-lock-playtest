using System;
using System.Collections.Generic;
using System.IO;
using ChronoLock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scripted Play Mode smoke test, not a replacement for human input/visual QA.
// Run with -executeMethod ChronoPlayCheck.Run -batchmode (without -quit or -nographics).
[InitializeOnLoad]
public static class ChronoPlayCheck
{
    const string RunningKey = "ChronoLock.ScriptedQA.Running";
    const string OutputKey = "ChronoLock.ScriptedQA.Output";
    static ChronoGame game;
    static CharacterController controller;
    static readonly List<string> errors = new List<string>();
    static readonly List<string> screenshots = new List<string>();
    static double started, stageAt;
    static float pausedElapsed, pausedRotor, vertical;
    static int stage, lastFrame = -1, initializedFrame, stageFrame;
    static bool initialized, finishing;
    static string output;

    static ChronoPlayCheck()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start scripted QA from Edit Mode.");
        output = Path.GetFullPath("Builds/PolishQA");
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--chrono-qa-out") output = Path.GetFullPath(args[i + 1]);
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/ChronoLock/Scenes/ChronoLab.unity");
        SessionState.SetBool("ChronoLock.PlayQA.HadBest",PlayerPrefs.HasKey("ChronoLock.BestTime.v1"));
        SessionState.SetFloat("ChronoLock.PlayQA.Best",PlayerPrefs.GetFloat("ChronoLock.BestTime.v1",0));
        SessionState.SetString(OutputKey, output);
        SessionState.SetBool(RunningKey, true);
        ResetStatics();
        Debug.Log("CHRONO_PLAY_QA_BEGIN scripted controller movement + production interaction APIs");
        EditorApplication.isPlaying = true;
    }

    static void ResetStatics()
    {
        game = null; controller = null; errors.Clear(); screenshots.Clear();
        stage = 0; lastFrame = -1; initializedFrame = 0;
        initialized = false; finishing = false; vertical = 0;
        started = stageAt = EditorApplication.timeSinceStartup;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(RunningKey, false) || finishing) return;
        // The isolated project has no Search index. This Editor-only startup failure
        // is retained in the Unity log but is unrelated to the runtime under test.
        if (stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false) || finishing) return;
        try
        {
            // Domain reload recreates static fields. The session flag and output survive it.
            if (started == 0) started = stageAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 90)
                throw new Exception("Overall timeout: stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (errors.Count > 0) throw new Exception("Runtime error: " + errors[0]);
            if (!initialized)
            {
                game = UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
                if (!game || !game.player || Time.frameCount < 3) return;
                // Awake/Start have now run on the serialized scene's fresh objects.
                game.player.enabled = false;
                controller = game.player.GetComponent<CharacterController>();
                Require(controller && controller.enabled && game.player.view, "Runtime scene references initialized");
                game.Restart(); // New title-screen boot requires an explicit run start.
                Require(game.GetComponent<ChronoFeedback>(), "Runtime synthesized audio component initialized");
                output = SessionState.GetString(OutputKey, Path.GetFullPath("Builds/PolishQA"));
                initialized = true; stage = -1; initializedFrame = Time.frameCount; stageAt = EditorApplication.timeSinceStartup;
                LookAt(new Vector3(0, 2.4f, 12));
                return;
            }
            if (StageSeconds > 18) throw new Exception("Stage timeout " + stage + " at " + game.player.transform.position);
            switch (stage)
            {
                case -1:
                    Settle();
                    if (Time.frameCount < initializedFrame + 3) break;
                    Capture("01-arrival-hud.png");
                    Next(); break;
                case 0:
                    Settle();
                    // CaptureScreenshot runs at the end of a frame. Keep the HUD unpaused until then.
                    if (StageSeconds < .2) break;
                    Require(!game.TryConfirmTarget(game.reactor) && !game.reactor.secured, "Unpowered reactor confirmation rejected");
                    game.TogglePause(); pausedElapsed = game.elapsed; pausedRotor = game.rotor.state;
                    Require(game.Paused, "Pause activates");
                    Next(); break;
                case 1:
                    if (StageSeconds < .3) break;
                    Require(Mathf.Approximately(pausedElapsed, game.elapsed) && Mathf.Approximately(pausedRotor, game.rotor.state), "Pause freezes timer and device state");
                    Require(!game.ApplyToTarget(game.rotor, TemporalMode.Freeze), "Paused interaction rejected");
                    game.TogglePause();
                    // Isolated hazard test: teleport only for this setup, then restart before the full route.
                    game.player.Teleport(new Vector3(0, 0, 9.3f));
                    Next(); break;
                case 2:
                    if (StageSeconds < .12) break;
                    Require(game.player.transform.position.z < 4 && !game.rotorCleared, "Live rotor danger restores arrival checkpoint");
                    game.Restart(); vertical = 0;
                    LookAt(new Vector3(0, 2.8f, 10));
                    Next(); break;
                case 3:
                    if(game.player.transform.position.z<2.92f){WalkTo(3);LookAt(new Vector3(0,2.8f,10));break;}
                    Settle();
                    if (StageSeconds < .25 || Time.frameCount < stageFrame + 3) break;
                    Require(game.CurrentTarget == game.rotor, "Arrival camera raycast acquires rotor");
                    Require(game.ApplyToTarget(game.CurrentTarget, TemporalMode.Freeze), "Freeze applied through production API");
                    LookAt(new Vector3(0, 1.6f, 22));
                    Next(); break;
                case 4:
                    if (!WalkTo(14)) break;
                    Require(game.rotorCleared && game.rotor.secured, "Controller crosses frozen rotor and locks checkpoint");
                    Require(game.ActiveDevice == null, "Secured rotor releases temporal link");
                    Next(); break;
                case 5:
                    if (!WalkTo(20)) break;
                    LookAt(new Vector3(-2.5f, 1.25f, 22));
                    Next(); break;
                case 6:
                    Settle();
                    // Editor wall time may advance during a stalled render without three
                    // gameplay frames refreshing ChronoGame.CurrentTarget after LookAt.
                    if (StageSeconds < .18 || Time.frameCount < stageFrame + 3) break;
                    if (game.CurrentTarget != game.reactor) DiagnoseAim("reactor");
                    Require(game.CurrentTarget == game.reactor, "Reactor console raycast acquired");
                    Require(game.ApplyToTarget(game.CurrentTarget, TemporalMode.Accelerate), "Acceleration applied through production API");
                    Next(); break;
                case 7:
                    Settle();
                    if (StageSeconds > 1 && screenshots.Count == 1) Capture("02-reactor-hud.png");
                    if (!game.reactor.Ready) break;
                    Require(StageSeconds < 9, "Accelerated reactor ready within nine seconds");
                    Require(game.TryConfirmTarget(game.reactor) && game.reactor.secured, "Reactor confirmation powers door");
                    LookAt(new Vector3(0, 1.6f, 35));
                    Next(); break;
                case 8:
                    Settle();
                    if (game.powerDoor.position.y < 5.95f) break;
                    Require(game.powerDoor.position.y >= 5.95f, "Powered bulkhead opens fully");
                    Next(); break;
                case 9:
                    if (!WalkTo(29)) break;
                    LookAt(new Vector3(-2.6f, 1.15f, 30));
                    Next(); break;
                case 10:
                    Settle();
                    if (StageSeconds < .18 || Time.frameCount < stageFrame + 3) break;
                    Require(game.CurrentTarget == game.bridge, "Bridge console raycast acquired");
                    Require(game.ApplyToTarget(game.CurrentTarget, TemporalMode.Rewind), "Rewind applied through production API");
                    Next(); break;
                case 11:
                    Settle();
                    if (!game.bridge.Ready) break;
                    Require(StageSeconds < 3, "Bridge incident memory restores within three seconds");
                    Require(game.TryConfirmTarget(game.bridge) && game.bridge.secured, "Bridge restoration secured");
                    LookAt(new Vector3(0, .4f, 35));
                    Next(); break;
                case 12:
                    Settle();
                    if (StageSeconds < .25) break;
                    Capture("03-restored-bridge-hud.png");
                    LookAt(new Vector3(0, 1.6f, 48));
                    Next(); break;
                case 13:
                    if (!WalkTo(41)) break;
                    Require(game.player.transform.position.y > -.5f, "Controller traverses restored bridge without falling");
                    Next(); break;
                case 14:
                    if (!WalkTo(46)) break;
                    Require(game.TryConfirmTarget(null) && game.Completed, "Full scripted walking route completes escape");
                    pausedElapsed = game.elapsed;
                    Next(); break;
                case 15:
                    if (StageSeconds < .2) break;
                    Require(Mathf.Approximately(pausedElapsed, game.elapsed), "Completed run timer remains frozen");
                    Capture("04-complete.png");
                    Next(); break;
                case 16:
                    if (StageSeconds < .3) break;
                    game.Restart(); vertical = 0;
                    Require(!game.Completed && !game.Paused && !game.rotorCleared && !game.reactor.secured && !game.bridge.secured && game.bridge.state > .999f, "Restart resets all puzzle flags");
                    Require(game.ActiveDevice == null && game.SelectedMode == TemporalMode.Freeze && game.player.transform.position.z < 4, "Restart resets player and active temporal link");
                    Next(); break;
                case 17:
                    if (StageSeconds < 1) break;
                    foreach (string file in screenshots)
                        if (!File.Exists(file) || new FileInfo(file).Length == 0)
                        { if (StageSeconds < 5) return; throw new Exception("Screenshot did not finish: " + file); }
                    Require(screenshots.Count == 4, "Four Play Mode screenshots saved");
                    Finish(true, "CHRONO_PLAY_QA_PASSED"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }

    static double StageSeconds => EditorApplication.timeSinceStartup - stageAt;
    static void Next() { stage++; stageAt = EditorApplication.timeSinceStartup; stageFrame = Time.frameCount; Debug.Log("CHRONO_PLAY_QA_STAGE " + stage); }
    static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Play Mode assertion failed: " + label);
        Debug.Log("PLAY_PASS " + label);
    }
    static void LookAt(Vector3 point) { game.player.view.transform.rotation = Quaternion.LookRotation(point - game.player.view.transform.position); }
    static void DiagnoseAim(string label)
    {
        var camera = game.player.view;
        string hitDescription = "none";
        if (Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 9))
            hitDescription = hit.collider.name + " point=" + hit.point + " distance=" + hit.distance + " device=" + hit.collider.GetComponentInParent<TemporalDevice>();
        Debug.Log("PLAY_AIM_DIAGNOSTIC " + label + " player=" + game.player.transform.position + " camera=" + camera.transform.position + " forward=" + camera.transform.forward + " playing=" + game.IsPlaying + " inputComponent=" + game.player.enabled + " target=" + game.CurrentTarget + " freshHit=" + hitDescription + " frame=" + Time.frameCount);
        foreach (var collider in game.reactor.GetComponentsInChildren<Collider>())
            Debug.Log("PLAY_REACTOR_COLLIDER " + collider.name + " bounds=" + collider.bounds + " enabled=" + collider.enabled);
        Capture("failure-" + label + ".png");
    }
    static void Settle() { Move(Vector3.zero); }
    static bool WalkTo(float z)
    {
        Vector3 pos = game.player.transform.position;
        if (pos.z >= z - .08f) { Settle(); return true; }
        Move(Vector3.forward * Mathf.Min(7, (z - pos.z) / Mathf.Max(.001f, Mathf.Min(Time.deltaTime, .1f))));
        return false;
    }
    static void Move(Vector3 horizontal)
    {
        float dt = Mathf.Min(Time.deltaTime, .1f);
        if (controller.isGrounded && vertical < 0) vertical = -2;
        vertical -= 18 * dt;
        controller.Move((horizontal + Vector3.up * vertical) * dt);
    }
    static void Capture(string name)
    {
        // Batch Editor has no presented Game view, so ScreenCapture never completes.
        // Render the gameplay camera explicitly; IMGUI HUD is checked in the visible build.
        string path = Path.Combine(output, name.Replace("-hud", "-world"));
        var camera=game.player.view;
        var target=new RenderTexture(1280,720,24);
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        Texture2D pixels=null;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());screenshots.Add(path);
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
        Debug.Log("CHRONO_PLAY_QA_SCREENSHOT " + path);
    }
    static void Finish(bool success, string message)
    {
        if (finishing) return;
        finishing = true; SessionState.SetBool(RunningKey, false);
        if (success) Debug.Log(message); else Debug.LogError("CHRONO_PLAY_QA_FAILED " + message);
        try
        {
            if (string.IsNullOrEmpty(output)) output = SessionState.GetString(OutputKey, Path.GetFullPath("Builds/PolishQA"));
            Directory.CreateDirectory(output);
            if(SessionState.GetBool("ChronoLock.PlayQA.HadBest",false)) PlayerPrefs.SetFloat("ChronoLock.BestTime.v1",SessionState.GetFloat("ChronoLock.PlayQA.Best",0));
            else PlayerPrefs.DeleteKey("ChronoLock.BestTime.v1");
            PlayerPrefs.Save();
            File.WriteAllText(Path.Combine(output, "playmode-result.txt"), message + "\nScripted CharacterController movement and production interaction API QA. Human mouse/keyboard input is not simulated. Images are camera renders; IMGUI HUD is not included. Unity Editor Search index startup error is retained in log and excluded from game failure results.\n" + string.Join("\n", errors));
        }
        catch (Exception fileError) { Debug.LogError(fileError); success = false; }
        EditorApplication.Exit(success ? 0 : 1);
    }
}



