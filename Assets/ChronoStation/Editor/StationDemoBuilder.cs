using System.IO;
using ChronoStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StationDemoBuilder
{
    const string ScenePath="Assets/ChronoStation/Scenes/ChronoStationDemo.unity";
    const string ArtPath="Assets/ChronoLock/Art/Polished/";
    static Material ivory,metal,dark,deck,white,cyan,amber,violet,glass,space,text;
    static readonly string[] Titles={"SELF / ACCELERATE","SELF / SLOW","SELF / STOP","SELF / REWIND","POWER RESTORATION","SERVICE TRANSIT","VENTILATION LOCK","RETURN VESSEL"};
    static readonly Vector3[] Terminals={new Vector3(0,1,4),new Vector3(-3,1,4),new Vector3(0,1,8),new Vector3(0,1,23),new Vector3(-2,1,9),new Vector3(-3,1,4),new Vector3(-3,1,9),new Vector3(-3,1,5)};

    [MenuItem("CHRONO STATION/Open Revised Demo")]
    public static void OpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); else BuildScene();
    }

    [MenuItem("CHRONO STATION/Rebuild Demo Scene")]
    public static void BuildScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/ChronoStation/Scenes");Directory.CreateDirectory("Assets/ChronoStation/Materials");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        ivory=Mat("Ivory",new Color(.71f,.76f,.76f),.12f,.42f);
        metal=Mat("Machined alloy",new Color(.16f,.22f,.27f),.72f,.46f);
        dark=Mat("Graphite",new Color(.024f,.037f,.055f),.35f,.35f);
        deck=Mat("Deck",new Color(.19f,.24f,.28f),.28f,.32f);
        white=Mat("Soft white diffuser",new Color(.8f,.9f,1),0,0,true);
        cyan=Mat("Cyan status",new Color(.025f,.68f,.8f),0,0,true);
        amber=Mat("Amber status",new Color(1,.4f,.07f),0,0,true);
        violet=Mat("Violet status",new Color(.46f,.24f,.95f),0,0,true);
        glass=Mat("Observation glass",new Color(.017f,.07f,.11f),.5f,.8f);
        space=Mat("Deep space",new Color(.001f,.002f,.009f),0,0,true);
        text=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChronoStation/Materials/World text.mat");
        if(!text){var shader=Shader.Find("ChronoLock/WorldText");if(!shader)shader=Shader.Find("GUI/Text Shader");text=new Material(shader);AssetDatabase.CreateAsset(text,"Assets/ChronoStation/Materials/World text.mat");}
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.19f,.25f,.33f);RenderSettings.ambientEquatorColor=new Color(.08f,.11f,.15f);RenderSettings.ambientGroundColor=new Color(.035f,.043f,.055f);
        RenderSettings.fog=false;
        var sun=new GameObject("Indirect station daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.85f;sun.color=new Color(.78f,.87f,1);sun.transform.rotation=Quaternion.Euler(38,-28,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;
        var game=new GameObject("CHRONO STATION / revised campaign").AddComponent<StationGame>();game.stages=new StationStage[8];
        for(int i=0;i<8;i++)game.stages[i]=Room(i);
        var player=new GameObject("Player");var cc=player.AddComponent<CharacterController>();cc.center=new Vector3(0,.9f,0);cc.height=1.8f;cc.radius=.32f;cc.stepOffset=.28f;cc.skinWidth=.035f;
        var camera=new GameObject("Player view").AddComponent<Camera>();camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,1.62f,0);camera.tag="MainCamera";camera.fieldOfView=78;camera.nearClipPlane=.05f;camera.farClipPlane=220;camera.backgroundColor=new Color(.005f,.01f,.025f);camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.gameObject.AddComponent<AudioListener>();
        var movement=player.AddComponent<StationPlayer>();movement.game=game;movement.view=camera;game.player=movement;camera.gameObject.AddComponent<StationHandView>().game=game;
        player.transform.position=new Vector3(-3,.05f,1);player.transform.rotation=Quaternion.Euler(0,20,0);
        PlayerSettings.productName="CHRONO STATION";PlayerSettings.companyName="5team";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();AssetDatabase.Refresh();Selection.activeGameObject=game.gameObject;
    }

    [MenuItem("CHRONO STATION/Build Windows Demo")]
    public static void BuildWindows()
    {
        if (!File.Exists(ScenePath)) BuildScene();Directory.CreateDirectory("Builds/ChronoStationDemo");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/ChronoStationDemo/CHRONO_STATION.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("CHRONO STATION Windows build failed: "+result.summary.result);
    }

    static StationStage Room(int index)
    {
        var root=new GameObject("ZONE "+(index+1).ToString("00")+" / "+Titles[index]).transform;root.position=new Vector3(index*80,0,0);var stage=root.gameObject.AddComponent<StationStage>();
        var accent=index<4?cyan:index<6?amber:violet;var panel=index<4?ivory:index<6?metal:deck;
        stage.entry=Marker(root,"Entry",new Vector3(0,.05f,2));stage.terminal=Marker(root,"Interaction point",Terminals[index]);stage.exit=Marker(root,"Exit interaction",index==3?new Vector3(-3,1,2):new Vector3(0,1,26));
        float gapA=index==1||index==5?6:index==7?7:-1;float gapB=index==1?14:index==5?18:index==7?15:-1;
        Floor(root,0,gapA<0?28:gapA);if(gapA>=0)Floor(root,gapB,28);
        Box(root,"Rear pressure hull",new Vector3(0,3.25f,-.25f),new Vector3(10.5f,6.5f,.5f),dark,true);
        Box(root,"Forward pressure hull",new Vector3(0,3.25f,28.25f),new Vector3(10.5f,6.5f,.5f),dark,true);
        Box(root,"Ceiling",new Vector3(0,6.65f,14),new Vector3(10,.3f,28),dark,true);
        for(int side=-1;side<=1;side+=2)
        {
            // Full-height collision boundary: there is no walkable rim around puzzle gaps.
            var hull=Box(root,"Continuous side collision",new Vector3(side*5.2f,3.25f,14),new Vector3(.4f,6.5f,28),dark,true);
            if(side==1&&(index==0||index==3||index==7))hull.GetComponent<Renderer>().enabled=false;
            for(int z=2;z<28;z+=4)
            {
                bool window=side==1&&(index==0||index==3||index==7)&&z>=10&&z<=22;
                if(!window)
                {
                    Model(root,"WallModule",new Vector3(side*4.99f,0,z),side<0?-90:90,4.5f,panel);
                    Box(root,"Wall upper panel",new Vector3(side*4.95f,5.3f,z),new Vector3(.11f,1.5f,3.88f),panel);
                }
                else
                {
                    Box(root,"Observation sill",new Vector3(4.94f,.48f,z),new Vector3(.3f,.96f,4),metal);
                    Box(root,"Observation header",new Vector3(4.94f,5.65f,z),new Vector3(.3f,1.7f,4),dark);
                    Box(root,"Window structural mullion",new Vector3(4.91f,3,z-2),new Vector3(.3f,4.5f,.12f),metal);
                }
                Box(root,"Upper recessed light",new Vector3(side*4.8f,4.67f,z),new Vector3(.035f,.055f,3.4f),accent);
                Beam(root,"Ceiling diagonal brace",new Vector3(side*4.86f,5.4f,z-1.8f),new Vector3(side*3.9f,6.3f,z-1.8f),.14f,metal);
                if(gapA<0||z+1.8f<=gapA||z-1.8f>=gapB)Box(root,"Deck edge guide",new Vector3(side*4.6f,.016f,z),new Vector3(.035f,.016f,3.5f),accent);
            }
        }
        for(int z=2;z<28;z+=4)
        {
            Box(root,"Ceiling cross rib",new Vector3(0,6.29f,z-1.8f),new Vector3(9.7f,.18f,.22f),metal);
            foreach(float x in new[]{-2.9f,2.9f}){Box(root,"Ceiling light body",new Vector3(x,6.35f,z),new Vector3(.6f,.18f,2.6f),metal);Box(root,"Ceiling light lens",new Vector3(x,6.25f,z),new Vector3(.38f,.018f,2.2f),white);}
            if(z%8==2)Point(root,new Vector3(0,4.8f,z),new Color(.84f,.92f,1),2.4f,9);
        }
        if(index==0||index==3||index==7)Vista(root,index);
        if(gapA>=0)
        {
            Threshold(root,gapA-.16f);Threshold(root,gapB+.16f);
            Box(root,"Lower machinery well",new Vector3(0,-5.5f,(gapA+gapB)/2),new Vector3(10,.4f,gapB-gapA),dark);
            for(int side=-1;side<=1;side+=2)for(float z=gapA+.5f;z<gapB;z+=2)Box(root,"Well hazard lamp",new Vector3(side*4.85f,-1.4f,z),new Vector3(.06f,.09f,.8f),amber);
        }
        Exit(root,stage,index,accent);
        if(index==0||index==2||index==4||index==6)Gate(root,stage,index==0?20:index==6?12:22,accent);
        Vector3 console=Terminals[index];console.y=0;if(Mathf.Abs(console.x)<.1f)console.x=-1.6f;
        if(index<4)Console(root,console,accent);
        Label(root,"0"+(index+1)+"  /  "+Titles[index],new Vector3(-4.45f,4.7f,27.91f),.095f,white.color);
        Label(root,index<4?"PERSONAL TEMPORAL SYNCHRONIZATION":index<6?"MAINTENANCE / EXTERNAL SYNCHRONIZATION":"ORBITAL TRANSFER / EARTH VECTOR",new Vector3(-4.45f,4.3f,27.9f),.043f,new Color(.5f,.7f,.76f));
        if(index==0){Model(root,"RescueCradle",new Vector3(3.35f,0,2.7f),180,2,ivory,true);Label(root,"RESCUE / ISOLATION",new Vector3(1.9f,2.5f,.06f),.055f,cyan.color,180);}
        if(index==2)
        {
            Box(root,"Inspection pad",new Vector3(0,.012f,8),new Vector3(3.8f,.02f,3.8f),metal);
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;Beam(root,"Inspection light ring",new Vector3(Mathf.Cos(a)*1.65f,.032f,8+Mathf.Sin(a)*1.65f),new Vector3(Mathf.Cos(b)*1.65f,.032f,8+Mathf.Sin(b)*1.65f),.026f,cyan);}
        }
        if(index>=4)Device(root,stage,index,accent);
        root.gameObject.SetActive(index==0);return stage;
    }

    static void Device(Transform root,StationStage stage,int index,Material accent)
    {
        var owner=Marker(root,"Temporal machinery",Vector3.zero);var device=owner.gameObject.AddComponent<StationTemporalObject>();stage.device=device;
        var cp=Terminals[index];cp.y=0;Console(owner,cp,accent);
        if(index==4)
        {
            device.kind=StationDeviceKind.Generator;device.displayName="시간 발전기";device.baseRate=.025f;
            device.movingPart=Marker(owner,"Generator rotating assembly",new Vector3(1.8f,0,11));Model(device.movingPart,"Reactor",Vector3.zero,0,3.5f,ivory);
            var collider=device.movingPart.gameObject.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,1.7f,0);collider.height=3.4f;collider.radius=1.15f;
            Point(owner,new Vector3(1.8f,2,11),new Color(.08f,.65f,.8f),1,4);
        }
        if(index==5)
        {
            device.kind=StationDeviceKind.MovingPlatform;device.displayName="이동 발판";device.baseRate=.32f;device.pingPong=true;device.endpointA=new Vector3(0,-.2f,7.5f);device.endpointB=new Vector3(0,-.2f,17);
            device.movingPart=Box(owner,"Moving service platform",device.endpointA,new Vector3(3,.4f,3.5f),metal,true);PlatformFinish(device.movingPart,new Vector3(3,.4f,3.5f),accent);
        }
        if(index==6)
        {
            device.kind=StationDeviceKind.Rotor;device.displayName="환기 장치";device.baseRate=.65f;device.movingPart=Marker(owner,"Rotor axis",new Vector3(0,2.7f,12));
            var target=device.movingPart.gameObject.AddComponent<BoxCollider>();target.size=new Vector3(5,5,.65f);target.isTrigger=true;
            Sphere(device.movingPart,"Bearing hub",Vector3.zero,new Vector3(.5f,.5f,.3f),metal);
            for(int i=0;i<6;i++){var spoke=Marker(device.movingPart,"Blade carrier",Vector3.zero);spoke.localRotation=Quaternion.Euler(0,0,i*60);var blade=Box(spoke,"Swept alloy blade",new Vector3(.16f,1.28f,0),new Vector3(.47f,1.85f,.1f),metal);blade.localRotation=Quaternion.Euler(0,0,-18);}
            for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36,b=(i+1)*Mathf.PI*2/36;Beam(owner,"Rotor perimeter",new Vector3(Mathf.Cos(a)*2.55f,2.7f+Mathf.Sin(a)*2.55f,12),new Vector3(Mathf.Cos(b)*2.55f,2.7f+Mathf.Sin(b)*2.55f,12),.15f,metal);}
        }
        if(index==7)
        {
            device.kind=StationDeviceKind.Cargo;device.displayName="이탈 연결 발판";device.baseRate=.16f;device.pingPong=false;device.endpointA=new Vector3(0,-.2f,11);device.endpointB=new Vector3(6,3.5f,11);
            device.movingPart=Box(owner,"Docking link bridge",device.endpointA,new Vector3(4,.4f,8.3f),metal,true);PlatformFinish(device.movingPart,new Vector3(4,.4f,8.3f),accent);
        }
    }
    static void PlatformFinish(Transform platform,Vector3 size,Material accent)
    {
        // Primitive scale is the platform's authored size, so children use normalized coordinates.
        Box(platform,"Inset tread",new Vector3(0,.51f,0),new Vector3(.9f,.02f,.94f),deck);
        foreach(float x in new[]{-.46f,.46f})Box(platform,"Safety edge",new Vector3(x,.53f,0),new Vector3(.025f,.025f,.94f),accent);
        for(int i=-3;i<=3;i++)Box(platform,"Grip line",new Vector3(0,.535f,i*.12f),new Vector3(.72f,.015f,.012f),dark);
    }
    static void Floor(Transform root,float start,float end)
    {
        Box(root,"Load-bearing deck",new Vector3(0,-.2f,(start+end)/2),new Vector3(10,.4f,end-start),dark,true);
        for(float z=start;z<end-.01f;z+=2)for(int x=-4;x<=4;x+=2)Box(root,"Deck cassette",new Vector3(x,-.009f,z+Mathf.Min(2,end-z)/2),new Vector3(1.975f,.016f,Mathf.Min(2,end-z)-.025f),deck);
    }
    static void Gate(Transform root,StationStage stage,float z,Material accent)
    {
        stage.gate=Box(root,"Timed pressure gate",new Vector3(0,2.2f,z),new Vector3(10,4.4f,.32f),dark,true);
        // Detail uses normalized coordinates because the gate root carries primitive scale.
        for(int side=-1;side<=1;side+=2)Box(stage.gate,"Sliding gate panel",new Vector3(side*.245f,0,-.56f),new Vector3(.475f,.94f,.17f),metal);
        Box(stage.gate,"Gate status band",new Vector3(0,.35f,-.67f),new Vector3(.86f,.016f,.025f),accent);
        Box(root,"Gate lintel",new Vector3(0,5.7f,z),new Vector3(10,1,.7f),metal);Threshold(root,z-.65f);
    }
    static void Exit(Transform root,StationStage stage,int index,Material accent)
    {
        Vector3 p=index==3?new Vector3(-3,0,.14f):new Vector3(0,0,27.85f);float yaw=index==3?180:0;
        var frame=Marker(root,"Exit transfer portal",p);frame.localRotation=Quaternion.Euler(0,yaw,0);
        Box(frame,"Exit sealed door",new Vector3(0,1.8f,0),new Vector3(2.5f,3.6f,.1f),metal);
        foreach(float x in new[]{-1.38f,1.38f})Box(frame,"Exit jamb",new Vector3(x,1.85f,-.09f),new Vector3(.22f,3.9f,.26f),ivory);
        Box(frame,"Exit header",new Vector3(0,3.78f,-.08f),new Vector3(3,.23f,.25f),ivory);
        stage.signal=Box(frame,"Exit availability",new Vector3(0,3.43f,-.075f),new Vector3(2.15f,.065f,.035f),accent).GetComponent<Renderer>();
        Label(frame,index==7?"EARTH / DEPARTURE":"TRANSFER / E",new Vector3(-.98f,2.6f,-.08f),.064f,white.color);
    }
    static void Console(Transform root,Vector3 p,Material accent)
    {
        var console=Model(root,"Console",p,0,1.3f,ivory);var hit=console.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.65f,0);hit.size=new Vector3(1.1f,1.3f,.85f);
        Box(root,"Console floor locator",p+new Vector3(0,.012f,-.6f),new Vector3(1.1f,.02f,.06f),accent);
    }
    static void Threshold(Transform root,float z){for(int x=-9;x<=9;x++){var stripe=Box(root,"Hazard hatch",new Vector3(x*.48f,.019f,z),new Vector3(.24f,.014f,.2f),amber);stripe.localRotation=Quaternion.Euler(0,-28,0);}}
    static void Vista(Transform root,int index)
    {
        Box(root,"Starfield backdrop",new Vector3(45,12,14),new Vector3(.1f,70,100),space);
        var random=new System.Random(931+index);for(int i=0;i<55;i++)Sphere(root,"Distant star",new Vector3(42,(float)random.NextDouble()*36-12,(float)random.NextDouble()*70-20),Vector3.one*(.035f+(float)random.NextDouble()*.07f),white);
        Sphere(root,"Blue planetary limb",new Vector3(38,-7,28),new Vector3(22,22,22),glass);
        Box(root,"Observation lower rail",new Vector3(4.8f,1,16),new Vector3(.18f,.12f,16),metal);
    }
    static Transform Model(Transform parent,string name,Vector3 p,float yaw,float height,Material panel,bool opening=false)
    {
        var wrapper=Marker(parent,name+" / imported Blender",p);var asset=AssetDatabase.LoadAssetAtPath<GameObject>((opening?"Assets/ChronoLock/Art/Opening/":ArtPath)+name+".fbx");
        if(asset)
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);model.transform.SetParent(wrapper,false);
            foreach(var c in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            Bounds bounds=new Bounds();bool first=true;foreach(var r in model.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){string n=mats[i]?mats[i].name:"";mats[i]=n.Contains("Teal")||n.Contains("Glyph")?cyan:n.Contains("Amber")?amber:n.Contains("Ceramic")||n.Contains("Cushion")?panel:n.Contains("Dark")?dark:metal;}r.sharedMaterials=mats;}
            if(!first&&bounds.size.y>.001f){float scale=height/bounds.size.y;Vector3 center=wrapper.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));model.transform.localPosition-=center;model.transform.localPosition*=scale;model.transform.localScale*=scale;}
        }
        else Box(wrapper,name+" placeholder",new Vector3(0,height/2,0),new Vector3(1,height,.65f),panel);
        wrapper.localRotation=Quaternion.Euler(0,yaw,0);return wrapper;
    }
    static Material Mat(string name,Color color,float metallic,float smooth,bool unlit=false)
    {
        string path="Assets/ChronoStation/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.color=color;material.SetColor("_BaseColor",color);if(!unlit){material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);}EditorUtility.SetDirty(material);return material;
    }
    static Transform Marker(Transform parent,string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
    static Transform Box(Transform parent,string name,Vector3 p,Vector3 scale,Material material,bool collision=false){return Primitive(parent,name,p,scale,material,PrimitiveType.Cube,collision);}
    static Transform Sphere(Transform parent,string name,Vector3 p,Vector3 scale,Material material){return Primitive(parent,name,p,scale,material,PrimitiveType.Sphere,false);}
    static Transform Primitive(Transform parent,string name,Vector3 p,Vector3 scale,Material material,PrimitiveType type,bool collision){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
    static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material){var t=Box(parent,name,(a+b)/2,new Vector3(width,Vector3.Distance(a,b),width),material);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Point(Transform parent,Vector3 p,Color color,float power,float range){var t=Marker(parent,"Local architectural light",p);var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=power;light.range=range;light.shadows=LightShadows.None;}
    static void Label(Transform parent,string value,Vector3 p,float size,Color color,float yaw=0){var t=Marker(parent,value,p);t.localRotation=Quaternion.Euler(0,yaw,0);var mesh=t.gameObject.AddComponent<TextMesh>();mesh.text=value;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=48;mesh.characterSize=size;mesh.anchor=TextAnchor.MiddleLeft;mesh.color=color;mesh.fontStyle=FontStyle.Bold;var material=new Material(text);material.mainTexture=mesh.font.material.mainTexture;string path="Assets/ChronoStation/Materials/Label_"+System.BitConverter.ToUInt32(System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)),0)+".mat";var stored=AssetDatabase.LoadAssetAtPath<Material>(path);if(stored){Object.DestroyImmediate(material);material=stored;}else AssetDatabase.CreateAsset(material,path);mesh.GetComponent<Renderer>().sharedMaterial=material;}
}
