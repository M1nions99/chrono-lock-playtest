using System;
using System.IO;
using ChronoLock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ChronoLabBuilder
{
    const string Folder = "Assets/ChronoLock";
    const string ScenePath = Folder + "/Scenes/ChronoLab.unity";
    static Material dark, wall, cyan, amber, violet, white;
    static Transform root;

    [MenuItem("ChronoLock/Create Prototype Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Folder + "/Scenes");
        Directory.CreateDirectory(Folder + "/Materials");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        root = new GameObject("CHRONO LOCK / LAB 07").transform;
        dark = Mat("Hull", new Color(.035f, .055f, .09f));
        wall = Mat("Panels", new Color(.21f, .27f, .34f));
        white = Mat("Ceramic", new Color(.66f, .73f, .79f));
        cyan = Mat("Temporal_Stop", new Color(.03f, .85f, .93f), true);
        amber = Mat("Temporal_Fast", new Color(1, .48f, .07f), true);
        violet = Mat("Temporal_Rewind", new Color(.68f, .26f, 1), true);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.35f, .43f, .58f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.025f, .04f, .07f);
        RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .013f;
        Box("Floor / Arrival", new Vector3(0, -.3f, 16), new Vector3(12, .6f, 32), dark);
        Box("Floor / Exit", new Vector3(0, -.3f, 45), new Vector3(12, .6f, 14), dark);
        Box("Left hull", new Vector3(-6.2f, 3, 26), new Vector3(.4f, 6, 52), wall);
        Box("Right hull", new Vector3(6.2f, 3, 26), new Vector3(.4f, 6, 52), wall);
        Box("Ceiling", new Vector3(0, 6.2f, 26), new Vector3(12.8f, .4f, 52), dark);
        Box("Arrival bulkhead", new Vector3(0, 3, -.2f), new Vector3(12, 6, .4f), wall);
        Box("Exit bulkhead", new Vector3(0, 3, 52), new Vector3(12, 6, .4f), wall);
        for (int z = 2; z < 52; z += 4)
        {
            Box("Ceiling rib", new Vector3(0, 5.8f, z), new Vector3(12, .3f, .2f), wall);
            Box("Left inset", new Vector3(-5.96f, 2.8f, z), new Vector3(.08f, 3.8f, 3.5f), dark);
            Box("Right inset", new Vector3(5.96f, 2.8f, z), new Vector3(.08f, 3.8f, 3.5f), dark);
            Box("Left guide", new Vector3(-5.85f, .3f, z), new Vector3(.12f, .08f, 2.8f), cyan, false);
            Box("Right guide", new Vector3(5.85f, .3f, z), new Vector3(.12f, .08f, 2.8f), cyan, false);
            if (z < 32 || z > 38)
                Box("Deck seam", new Vector3(0, .006f, z), new Vector3(11.7f, .012f, .04f), wall, false);
            if (z % 8 == 2) Lamp(new Vector3(0, 5, z), new Color(.55f, .76f, 1));
        }
        var game = root.gameObject.AddComponent<ChronoGame>();

        var rotor = new GameObject("01 / Temporal rotor"); rotor.transform.SetParent(root);
        var rotation = new GameObject("Rotor visual").transform;
        rotation.SetParent(rotor.transform); rotation.localPosition = new Vector3(0, 2.8f, 10);
        for (int i = 0; i < 4; i++)
        {
            var blade = Box("Rotor blade", Vector3.zero, new Vector3(.38f, 4.5f, .25f), cyan, false);
            blade.transform.SetParent(rotation, false); blade.transform.localPosition = Vector3.zero;
            blade.transform.localRotation = Quaternion.Euler(0, 0, i * 45);
        }
        var rotorConsole = Box("STOP / aim here", new Vector3(-2, 1.25f, 7.3f), new Vector3(1.2f, 2.5f, .7f), cyan);
        rotorConsole.transform.SetParent(rotor.transform, true);
        var rotorTarget = rotor.AddComponent<TemporalDevice>(); rotorTarget.Initialize(DeviceKind.Rotor, rotation, "환기 로터");
        // The visual blades do not snag a CharacterController; the entire danger plane is evaluated by ChronoGame.
        var aimZone = Box("Rotor aim volume", new Vector3(0, 2.8f, 10), new Vector3(10, 5.6f, .35f), dark);
        aimZone.GetComponent<Renderer>().enabled = false;
        aimZone.GetComponent<Collider>().isTrigger = true;
        aimZone.transform.SetParent(rotor.transform, true);
        Label("01   /   FREEZE", new Vector3(-2.6f, 4.7f, 9.8f), .28f, cyan.color);
        Label("AIM + [1] + CLICK", new Vector3(-2.5f, 3.9f, 9.8f), .16f, Color.white);

        var reactor = new GameObject("02 / Reactor"); reactor.transform.SetParent(root);
        var pedestal = Box("Reactor console", new Vector3(-2.5f, 1, 22), new Vector3(2, 2, 1.5f), wall);
        pedestal.transform.SetParent(reactor.transform, true);
        var core = Box("Charge core", new Vector3(-2.5f, 2.7f, 22), Vector3.one, amber, false);
        core.transform.SetParent(reactor.transform, true);
        var reactorTarget = reactor.AddComponent<TemporalDevice>(); reactorTarget.Initialize(DeviceKind.Reactor, core.transform, "보조 발전기");
        Label("02   /   ACCELERATE", new Vector3(-4.8f, 4.6f, 24), .25f, amber.color);
        Label("FULL CHARGE > [E]", new Vector3(-4.2f, 3.9f, 24), .16f, Color.white);
        game.powerDoor = Bulkhead("Power interlock", 27, amber);

        var bridge = new GameObject("03 / Recorded bridge"); bridge.transform.SetParent(root);
        var deck = Box("Rewindable deck", new Vector3(0, 0, 35), new Vector3(4, .4f, 6.3f), white);
        deck.transform.SetParent(bridge.transform, true);
        var bridgeConsole = Box("REWIND / aim here", new Vector3(-2.6f, 1.15f, 30), new Vector3(1.1f, 2.3f, .8f), violet);
        bridgeConsole.transform.SetParent(bridge.transform, true);
        var bridgeTarget = bridge.AddComponent<TemporalDevice>(); bridgeTarget.Initialize(DeviceKind.Bridge, deck.transform, "손상된 연결 다리");
        Label("03   /   REWIND", new Vector3(-2.6f, 4.5f, 37.8f), .28f, violet.color);
        Label("RESTORE > [E]", new Vector3(-2, 3.7f, 37.8f), .17f, Color.white);
        Box("Gap warning left", new Vector3(-4.1f, .05f, 31.7f), new Vector3(3.5f, .1f, .2f), amber);
        Box("Gap warning right", new Vector3(4.1f, .05f, 31.7f), new Vector3(3.5f, .1f, .2f), amber);
        game.exitDoor = Bulkhead("Escape airlock", 48, cyan);
        Label("ESCAPE   /   [E]", new Vector3(-2.7f, 4.8f, 47.7f), .3f, cyan.color);
        Label("CHRONO / LOCK", new Vector3(-2.5f, 4.5f, 5), .28f, Color.white);
        game.rotor = rotorTarget; game.reactor = reactorTarget; game.bridge = bridgeTarget;

        var player = new GameObject("Player"); player.transform.SetParent(root);
        player.transform.position = new Vector3(0, 1, 3);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = .32f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .3f;
        var camera = new GameObject("Player Camera"); camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = new Vector3(0, 1.62f, 0); camera.tag = "MainCamera";
        var view = camera.AddComponent<Camera>(); view.fieldOfView = 80; view.nearClipPlane = .05f;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.02f, .03f, .055f);
        camera.AddComponent<AudioListener>();
        var movement = player.AddComponent<ChronoPlayer>(); movement.view = view; movement.game = game; game.player = movement;
        camera.AddComponent<ChronoWrist>().game = game;
        ChronoLabArt.Upgrade(game);
        rotor.AddComponent<TemporalGlow>().game = game;
        reactor.AddComponent<TemporalGlow>().game = game;
        bridge.AddComponent<TemporalGlow>().game = game;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("CHRONO_SCENE_CREATED " + ScenePath);
        if (!Application.isBatchMode) Selection.activeGameObject = root.gameObject;
    }
    static Transform Bulkhead(string name, float z, Material accent)
    {
        Box(name + " left", new Vector3(-4, 3, z), new Vector3(4, 6, .6f), wall);
        Box(name + " right", new Vector3(4, 3, z), new Vector3(4, 6, .6f), wall);
        Box(name + " top", new Vector3(0, 5, z), new Vector3(4, 2, .6f), wall);
        Box(name + " strip left", new Vector3(-2.1f, 2, z - .35f), new Vector3(.12f, 4, .08f), accent, false);
        Box(name + " strip right", new Vector3(2.1f, 2, z - .35f), new Vector3(.12f, 4, .08f), accent, false);
        return Box(name + " door", new Vector3(0, 2, z), new Vector3(4, 4, .5f), dark).transform;
    }
    static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(root); go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Lamp(Vector3 position, Color color)
    {
        Box("Ceiling light", position + Vector3.up * .65f, new Vector3(3, .08f, .25f), cyan, false);
        var go = new GameObject("Area fill"); go.transform.SetParent(root); go.transform.position = position;
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color;
        light.range = 13; light.intensity = 3; light.shadows = LightShadows.None;
    }
    static void Label(string text, Vector3 position, float size, Color color)
    {
        var go = new GameObject(text); go.transform.SetParent(root); go.transform.position = position;
        var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size * .35f; mesh.fontSize = 48; mesh.color = color;
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        const string fontPath = Folder + "/Materials/WorldText.mat";
        var textMaterial = AssetDatabase.LoadAssetAtPath<Material>(fontPath);
        if (!textMaterial)
        {
            textMaterial = new Material(Shader.Find("ChronoLock/WorldText"));
            textMaterial.mainTexture = mesh.font.material.mainTexture;
            AssetDatabase.CreateAsset(textMaterial, fontPath);
        }
        go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
    }
    static Material Mat(string name, Color color, bool emissive = false)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing)
        {
            if (emissive) { existing.shader = Shader.Find("Universal Render Pipeline/Unlit"); existing.SetColor("_BaseColor", color); EditorUtility.SetDirty(existing); }
            return existing;
        }
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        if (emissive) { material.shader = Shader.Find("Universal Render Pipeline/Unlit"); material.SetColor("_BaseColor", color); }
        AssetDatabase.CreateAsset(material, path); return material;
    }

    // Runs without touching the user's open project when invoked on a verification copy.
    public static void ValidatePrototype()
    {
        CreateScene();
        var game = UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
        Require(game && game.player && game.rotor && game.reactor && game.bridge, "Scene references");
        var r = game.rotor; r.SetMode(TemporalMode.Normal); r.Tick(.2f); float angle = r.state;
        r.SetMode(TemporalMode.Freeze); r.Tick(.2f); Require(Mathf.Approximately(r.state, angle), "Freeze preserves state");
        r.SetMode(TemporalMode.Rewind); r.Tick(.2f); Require(r.state < angle, "Rewind restores recorded rotor state");
        r.ResetDevice(); r.SetMode(TemporalMode.Accelerate); r.Tick(.2f); Require(r.state > angle * 3.5f, "Acceleration is local x4");
        var reactor = game.reactor; reactor.ResetDevice(); reactor.SetMode(TemporalMode.Accelerate);
        for (int i = 0; i < 250; i++) reactor.Tick(1f / 30f);
        Require(reactor.Ready, "Reactor charges within nine seconds accelerated");
        reactor.SetMode(TemporalMode.Rewind); reactor.Tick(.2f); Require(reactor.state < 1, "Reactor recorded charge rewinds");
        var bridge = game.bridge; bridge.SetMode(TemporalMode.Rewind);
        for (int i = 0; i < 65; i++) bridge.Tick(1f / 30f);
        Require(bridge.Ready && Mathf.Abs(bridge.movingPart.localPosition.y) < .01f, "Bridge restores authored incident memory");
        bridge.Secure(); bridge.SetMode(TemporalMode.Accelerate); bridge.Tick(.2f); Require(bridge.Ready, "Secured bridge remains usable");
        r.ResetDevice(); for (int i = 0; i < 600; i++) r.Tick(1f / 30f);
        Require(r.HistoryCount <= 241, "History is bounded to eight seconds");
        game.Restart(); Require(!game.rotorCleared && !game.reactor.secured && game.bridge.state == 1, "Restart resets puzzle state");
        // Reload verifies actual serialized scene references, not just the builder's in-memory objects.
        EditorSceneManager.OpenScene(ScenePath);
        game = UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
        Require(game.player.view && game.bridge.movingPart && game.powerDoor && game.exitDoor, "Scene survives serialization");
        Debug.Log("CHRONO_VALIDATION_PASSED");
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception("CHRONO validation failed: " + message); Debug.Log("PASS " + message); }

    public static void BuildWindows()
    {
        EditorSceneManager.OpenScene(ScenePath);
        PlayerSettings.productName = "CHRONO LOCK";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.defaultIsNativeResolution = false;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Builds/ChronoLock/ChronoLock.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("ChronoLock build failed: " + report.summary.result);
        Debug.Log("CHRONO_WINDOWS_BUILD_PASSED");
    }

    public static void VerifyPreviewAndBuild()
    {
        ValidatePrototype();
        var game = UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
        game.player.Teleport(new Vector3(0, .05f, 3));
        var camera = game.player.view;
        var texture = new RenderTexture(1280, 720, 24);
        var old = RenderTexture.active;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes("Builds/ChronoLab-preview.png", pixels.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old;
        UnityEngine.Object.DestroyImmediate(pixels); texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
        EditorSceneManager.OpenScene(ScenePath);
        BuildWindows();
    }
}
