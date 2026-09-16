using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoLock;
using ChronoStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Runs only in a separate batch Editor. Owned virtual devices feed the real gameplay Update.
[InitializeOnLoad]
public static class StationControlCheck
{
    const string Prefix = "Station.ControlQA.";
    struct Packet { public MouseState mouse; public KeyboardState keyboard; }
    static readonly Queue<Packet> packets = new Queue<Packet>();
    static readonly List<string> checks = new List<string>();
    static readonly List<string> trace = new List<string>();
    static StationGame game;
    static Mouse mouse, previousMouse;
    static Keyboard keyboard, previousKeyboard;
    static InputSettings originalInputSettings, batchInputSettings;
    static MouseState heldMouse;
    static KeyboardState heldKeyboard;
    static int phase, frame = -1, waited, serial;
    static int lastTracedPhase = -1, expectedLeft, expectedRight, receivedLeft, receivedRight;
    static double began;
    static StationAbility selectedBeforePause;
    static string Output => SessionState.GetString(Prefix + "Output", "Builds/StationControlQA");

    static StationControlCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("StationControlCheck requires a separate batch Unity process without -quit.");
        string output = Path.GetFullPath("Builds/StationControlQA");
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--chrono-controls-out") output = Path.GetFullPath(args[i + 1]);
        SessionState.SetString(Prefix + "Output", output);
        SessionState.SetString(Prefix + "Runtime", JsonUtility.ToJson(ChronoPreferences.Current));
        SessionState.SetBool(Prefix + "HadKey", PlayerPrefs.HasKey(ChronoPreferences.StorageKey));
        SessionState.SetString(Prefix + "Raw", PlayerPrefs.GetString(ChronoPreferences.StorageKey, ""));
        EditorSceneManager.OpenScene("Assets/ChronoStation/Scenes/ChronoStationDemo.unity");
        SessionState.SetBool(Prefix + "Running", true);
        EditorApplication.isPlaying = true;
    }
    static MouseState BlankMouse() => new MouseState { position = new Vector2(-100, -100) };
    static void Queue(MouseState m, KeyboardState k) { packets.Enqueue(new Packet { mouse = m, keyboard = k }); }
    static void ReleaseInput() { Queue(BlankMouse(), new KeyboardState()); }
    static void Tap(MouseButton button)
    {
        if (button == MouseButton.Left) expectedLeft++;
        if (button == MouseButton.Right) expectedRight++;
        Queue(BlankMouse().WithButton(button), new KeyboardState()); ReleaseInput();
    }
    static void Tap(Key key) { Queue(BlankMouse(), new KeyboardState(key)); ReleaseInput(); }
    static void Scroll(float amount)
    {
        var state = BlankMouse(); state.scroll = new Vector2(0, amount);
        Queue(state, new KeyboardState()); ReleaseInput();
    }
    static void FeedInput()
    {
        // Queue before the normal Dynamic update flushes its events, never run an extra input update.
        if (!SessionState.GetBool(Prefix + "Running", false) || InputState.currentUpdateType != InputUpdateType.Dynamic || mouse == null || keyboard == null) return;
        if (packets.Count > 0)
        {
            var packet = packets.Dequeue(); heldMouse = packet.mouse; heldKeyboard = packet.keyboard;
            Trace("QUEUE buttons=" + heldMouse.buttons + " wheel=" + heldMouse.scroll.y + " pending=" + packets.Count);
        }
        mouse.MakeCurrent(); keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(mouse, heldMouse);
        InputSystem.QueueStateEvent(keyboard, heldKeyboard);
        heldMouse.scroll = Vector2.zero;
    }
    static void ObserveInput()
    {
        if (!SessionState.GetBool(Prefix + "Running", false) || InputState.currentUpdateType != InputUpdateType.Dynamic || mouse == null || keyboard == null) return;
        bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;
        if (left) receivedLeft++;
        if (right) receivedRight++;
        if (left || right || mouse.scroll.ReadValue() != Vector2.zero || keyboard.anyKey.wasPressedThisFrame)
            Trace("DELIVERED leftEdge=" + left + " rightEdge=" + right + " wheel=" + mouse.scroll.ReadValue().y + " anyKeyEdge=" + keyboard.anyKey.wasPressedThisFrame);
    }
    static void Trace(string message)
    {
        string state = "frame=" + Time.frameCount + " phase=" + phase + " " + message
            + " currentMouse=" + (Mouse.current == null ? "none" : Mouse.current.name)
            + " virtualEnabled=" + (mouse != null && mouse.enabled)
            + " leftEdges=" + receivedLeft + "/" + expectedLeft + " rightEdges=" + receivedRight + "/" + expectedRight
            + " playing=" + (game && game.IsPlaying) + " accepts=" + (game && game.AcceptsInput)
            + " self=" + (game ? game.SelfActive.ToString() : "none") + " cast=" + (game ? game.CastSerial : -1)
            + " cooldown=" + (game ? game.Cooldown.ToString("0.000") : "none");
        if (trace.Count < 220) trace.Add(state);
        Debug.Log("STATION_CONTROL_TRACE " + state);
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks.Add(message); Debug.Log("STATION_CONTROL_PASS " + message);
    }
    static void Next() { phase++; waited = 0; }
    static void Stage(int index)
    {
        // Isolate input coverage without setting solved flags or changing ability implementations.
        typeof(StationGame).GetMethod("SelectStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { index });
        if (index == 4) FaceDevice();
    }
    static void FaceDevice()
    {
        var device = game.CurrentStage.device;
        Collider closest = null; float distance = float.MaxValue;
        foreach (var collider in device.GetComponentsInChildren<Collider>())
        {
            if (collider.transform == device.movingPart || collider.transform.IsChildOf(device.movingPart)) continue;
            float current = Vector3.Distance(collider.bounds.center, game.Player.View.transform.position);
            if (current < distance) { closest = collider; distance = current; }
        }
        game.Player.LookAt(closest ? closest.bounds.center : device.movingPart.position);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (began == 0) began = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - began > 45) throw new Exception("Control QA timed out at phase " + phase + "; pending events=" + packets.Count);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!game)
            {
                game = UnityEngine.Object.FindFirstObjectByType<StationGame>();
                if (!game || !game.GetComponent<StationUI>()) { game = null; return; }
                // Headless Editors do not focus a Game View. Default input settings would route
                // mouse/keyboard packets into Editor updates instead of gameplay. Use an unsaved
                // settings clone for this process and restore the exact original object on exit.
                originalInputSettings = InputSystem.settings;
                batchInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
                batchInputSettings.hideFlags = HideFlags.HideAndDontSave;
                batchInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                batchInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = batchInputSettings;
                previousMouse = Mouse.current; previousKeyboard = Keyboard.current;
                mouse = InputSystem.AddDevice<Mouse>("StationControlQAMouse");
                keyboard = InputSystem.AddDevice<Keyboard>("StationControlQAKeyboard");
                heldMouse = BlankMouse(); heldKeyboard = new KeyboardState();
                InputSystem.onBeforeUpdate += FeedInput;
                InputSystem.onAfterUpdate += ObserveInput;
                Time.captureDeltaTime = 1f / 60;
                game.Player.AutomatedInput = true;
                game.NewGame(); game.SkipOpening(); waited = 0; return;
            }
            if (++waited < 5 || packets.Count > 0 || !game.AcceptsInput) return;
            if (lastTracedPhase != phase) { lastTracedPhase = phase; Trace("ASSERT"); }
            if (receivedLeft < expectedLeft || receivedRight < expectedRight)
                throw new Exception("Queued click did not reach the normal Dynamic input update: left " + receivedLeft + "/" + expectedLeft + ", right " + receivedRight + "/" + expectedRight);
            switch (phase)
            {
                case 0:
                    Check(game.Step == 0 && !game.ObjectUnlocked, "Opening lesson locks object powers");
                    serial = game.CastSerial; Tap(MouseButton.Left); Next(); break;
                case 1:
                    Check(!game.SelfActive.HasValue && !game.ActiveObject && game.CastSerial == serial, "Left click before object unlock never casts on self");
                    Tap(MouseButton.Right); Next(); break;
                case 2:
                    Check(game.SelfActive == StationAbility.Accelerate, "Right click activates self acceleration in the opening lesson");
                    serial = game.CastSerial; Tap(MouseButton.Left); Next(); break;
                case 3:
                    Check(game.SelfActive == StationAbility.Accelerate && game.CastSerial == serial, "Locked left click does not toggle or refresh an existing self effect");
                    Tap(Key.Q); Next(); break;
                case 4:
                    Check(!game.SelfActive.HasValue, "Q cancels the self effect"); Scroll(120); Next(); break;
                case 5:
                    Check(game.Selected == StationAbility.Accelerate, "Wheel wraps only the single unlocked ability"); Tap(Key.Digit4); Next(); break;
                case 6:
                    Check(game.Selected == StationAbility.Accelerate, "Number keys cannot select a locked ability"); Stage(1); Next(); break;
                case 7:
                    Check(game.UnlockedAbilities == 2 && game.Selected == StationAbility.Slow, "Second lesson exposes exactly two choices"); Scroll(-120); Next(); break;
                case 8:
                    Check(game.Selected == StationAbility.Accelerate, "Negative wheel selects previous unlocked ability"); Scroll(-120); Next(); break;
                case 9:
                    Check(game.Selected == StationAbility.Slow, "Negative wheel wraps inside the unlocked subset"); Scroll(120); Next(); break;
                case 10:
                    Check(game.Selected == StationAbility.Accelerate, "Positive wheel wraps inside the unlocked subset"); Stage(4); Next(); break;
                case 11:
                    Check(game.ObjectUnlocked && game.Focused == game.CurrentStage.device, "Production targeting ray identifies the generator after object unlock");
                    Tap(MouseButton.Right); Next(); break;
                case 12:
                    Check(game.SelfActive == StationAbility.Accelerate && !game.ActiveObject, "Right click targets self even while aiming at a device");
                    Tap(MouseButton.Left); Next(); break;
                case 13:
                    Check(game.ActiveObject == game.CurrentStage.device && game.ActiveObject.ActiveAbility == StationAbility.Accelerate && game.SelfActive == StationAbility.Accelerate, "Left click affects the aimed object and preserves the self effect");
                    Tap(Key.Q); Next(); break;
                case 14:
                    if (game.Cooldown > 0) break;
                    Check(!game.SelfActive.HasValue && !game.ActiveObject && !game.CurrentStage.device.ActiveAbility.HasValue, "Q releases self and object effects together");
                    Tap(MouseButton.Right); Next(); break;
                case 15:
                    Check(game.SelfActive == StationAbility.Accelerate, "A fresh right click starts self effect after cancel cooldown");
                    Tap(MouseButton.Right); Next(); break;
                case 16:
                    Check(!game.SelfActive.HasValue, "Repeating the same right click toggles self effect off");
                    Tap(MouseButton.Left); Next(); break;
                case 17:
                    Check(game.ActiveObject == game.CurrentStage.device, "A fresh left click starts the object effect");
                    Tap(MouseButton.Left); Next(); break;
                case 18:
                    if (game.Cooldown > 0) break;
                    Check(!game.ActiveObject && !game.CurrentStage.device.ActiveAbility.HasValue, "Repeating the same left click toggles object effect off");
                    Tap(Key.Digit3); Next(); break;
                case 19:
                    Check(game.Selected == StationAbility.Stop, "Key 3 selects stop"); Tap(MouseButton.Right); Next(); break;
                case 20:
                    Check(game.SelfActive == StationAbility.Stop, "Right click starts self stop"); Tap(Key.Digit2); Next(); break;
                case 21:
                    Check(game.Selected == StationAbility.Slow && game.SelfActive == StationAbility.Stop, "Selection can change while self stop remains active");
                    Tap(MouseButton.Right); Next(); break;
                case 22:
                    Check(!game.SelfActive.HasValue, "Right click always releases self stop even after selecting another ability");
                    game.Player.LookAt(game.Player.View.transform.position + new Vector3(0, 1, -10)); Next(); break;
                case 23:
                    Check(!game.Focused, "Empty-target fixture uses the real raycast"); serial = game.CastSerial; Tap(MouseButton.Left); Next(); break;
                case 24:
                    Check(!game.SelfActive.HasValue && !game.ActiveObject && game.CastSerial == serial, "Left click into empty space never falls back to self casting");
                    game.Pause(); Next(); break;
                case 25:
                    Check(game.Page == StationPage.Pause, "Pause opens before held-input test"); selectedBeforePause = game.Selected; serial = game.CastSerial;
                    var held = BlankMouse().WithButton(MouseButton.Left).WithButton(MouseButton.Right); held.scroll = new Vector2(0, 120);
                    expectedLeft++; expectedRight++;
                    Queue(held, new KeyboardState(Key.Digit3)); Next(); break;
                case 26:
                    Check(game.Page == StationPage.Pause && !game.SelfActive.HasValue && !game.ActiveObject && game.CastSerial == serial && game.Selected == selectedBeforePause, "Paused mouse buttons, wheel, and number key cannot alter gameplay");
                    game.Resume(); Next(); break;
                case 27:
                    Check(game.IsPlaying && !game.SelfActive.HasValue && !game.ActiveObject && game.CastSerial == serial && game.Selected == selectedBeforePause, "Holding buttons across resume does not replay paused input");
                    ReleaseInput(); Next(); break;
                case 28:
                    // Canceling self stop started a real cooldown, which correctly froze during
                    // pause. Wait it out before testing whether a fresh post-resume click works.
                    if (game.Cooldown > 0) break;
                    Tap(MouseButton.Right); Next(); break;
                case 29:
                    Check(game.SelfActive == StationAbility.Slow, "A fresh click after resume works normally"); Tap(Key.Q); Next(); break;
                case 30:
                    Check(!game.SelfActive.HasValue, "Q still cancels after resume"); Tap(Key.Digit1); Next(); break;
                case 31:
                    Check(game.Selected == StationAbility.Accelerate, "Key 1 selects accelerate"); Tap(Key.Digit4); Next(); break;
                case 32:
                    Check(game.Selected == StationAbility.Rewind, "Key 4 selects rewind once unlocked"); Scroll(120); Next(); break;
                case 33:
                    Check(game.Selected == StationAbility.Accelerate, "Forward wheel wraps all four unlocked abilities"); Scroll(-120); Next(); break;
                case 34:
                    Check(game.Selected == StationAbility.Rewind, "Backward wheel wraps all four unlocked abilities");
                    Finish(true, "STATION_CONTROL_QA_PASSED"); break;
            }
        }
        catch (Exception e) { Finish(false, e + "\nPhase=" + phase); }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Prefix + "Running", false) || stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Finish(bool success, string message)
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        SessionState.SetBool(Prefix + "Running", false);
        InputSystem.onBeforeUpdate -= FeedInput;
        InputSystem.onAfterUpdate -= ObserveInput;
        // Error logs can arrive from inside Input System dispatch. Remove devices only after
        // that dispatch has returned, so cleanup never mutates its active device iteration.
        EditorApplication.delayCall += () => CompleteExit(success, message);
    }
    static void CompleteExit(bool success, string message)
    {
        try
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (originalInputSettings) InputSystem.settings = originalInputSettings;
            if (batchInputSettings) UnityEngine.Object.DestroyImmediate(batchInputSettings);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            Time.captureDeltaTime = 0;
            ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(SessionState.GetString(Prefix + "Runtime", "{}")));
            bool prefsUnchanged = PlayerPrefs.HasKey(ChronoPreferences.StorageKey) == SessionState.GetBool(Prefix + "HadKey", false)
                && PlayerPrefs.GetString(ChronoPreferences.StorageKey, "") == SessionState.GetString(Prefix + "Raw", "");
            if (!prefsUnchanged) { success = false; message += "\nStored preferences changed unexpectedly."; }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "controls-result.txt"), message + "\n" + string.Join("\n", checks) + "\nQueued virtual Mouse/Keyboard states are processed by normal Input System Dynamic updates and production StationGame.Update. A temporary in-memory InputSettings clone routes input into the headless Game View; the original settings object is restored without saving any asset. Button edges must be observed after Dynamic input processing before gameplay assertions. Fixture selects authored stages but never fakes target focus, solved flags, or ability outcomes. No physical devices disabled or removed; only owned virtual devices are removed. Runtime settings restored without saving preferences.\n\nINPUT DELIVERY TRACE\n" + string.Join("\n", trace));
        }
        finally { EditorApplication.Exit(success ? 0 : 1); }
    }
}
