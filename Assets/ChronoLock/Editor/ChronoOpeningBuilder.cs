using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds only the owned rescue wing. Existing puzzle rooms, references, and art remain intact.
public static class ChronoOpeningBuilder
{
    const string ScenePath="Assets/ChronoLock/Scenes/ChronoLab.unity";
    const string RootName="ARKA / Rescue Bay";
    const string Folder="Assets/ChronoLock/Materials/Opening/";
    const string CradlePath="Assets/ChronoLock/Art/Opening/RescueCradle.fbx";
    const string WallPath="Assets/ChronoLock/Art/Polished/WallModule.fbx";
    static Transform root;
    static Material hull,deck,ceramic,metal,teal,amber,white,sky,black,soft;

    [MenuItem("ChronoLock/Upgrade Existing Scene - Rescue Opening")]
    public static void Upgrade()
    {
        AssetDatabase.Refresh();
        var cradle=AssetDatabase.LoadAssetAtPath<GameObject>(CradlePath);
        if(!cradle) throw new InvalidOperationException("Opening not modified: required Blender cradle missing: "+CradlePath);
        if(!File.Exists(ScenePath)) throw new FileNotFoundException("Existing ChronoLab scene is required.",ScenePath);
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Refusing to replace a dirty active scene in batch mode.");
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var arrival=Find(scene,"Arrival bulkhead");
        if(!arrival) throw new InvalidOperationException("Expected Arrival bulkhead was not found. Existing map was not modified.");
        if(Vector3.Distance(arrival.transform.position,new Vector3(0,3,-.2f))>.1f)
            throw new InvalidOperationException("Arrival bulkhead position differs from the authored map; refusing to alter it.");
        string backup="Builds/OpeningBackups/ChronoLab-before-opening-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(ScenePath,backup,false);
        File.WriteAllText(backup+".txt","Pre-opening scene backup. Builder only regenerates '"+RootName+"' and disables the exact Arrival bulkhead at (0,3,-0.2). Other rooms remain authored. Materials/Opening belongs to this builder.");
        var existing=Find(scene,RootName);if(existing)UnityEngine.Object.DestroyImmediate(existing);
        root=new GameObject(RootName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject,scene);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        hull=Mat("Hull",new Color(.028f,.048f,.065f),false,.55f);
        deck=Mat("Deck",new Color(.13f,.19f,.22f),false,.5f);
        ceramic=Mat("Ceramic",new Color(.54f,.66f,.67f),false,.2f);
        metal=Mat("Metal",new Color(.13f,.22f,.25f),false,.8f);
        teal=Mat("Teal",new Color(.12f,.84f,.77f),true);
        amber=Mat("Amber",new Color(1,.39f,.085f),true);
        white=Mat("White",new Color(.66f,.91f,.94f),true);
        sky=Mat("Space",new Color(.001f,.002f,.009f),true);
        black=Mat("EventHorizon",Color.black,true);
        soft=Mat("Soft",new Color(.095f,.16f,.21f),false,.02f);
        arrival.SetActive(false);
        Architecture();InstallCradle(cradle);Medical();Window();Lighting();
        Marker("Opening standing pose",new Vector3(0,0,-7),Quaternion.identity);
        Marker("Opening wake pose",new Vector3(0,1.45f,-10.2f),Quaternion.Euler(-25,0,0));
        Marker("Opening exit marker",new Vector3(0,0,1.5f),Quaternion.identity);
        var game=UnityEngine.Object.FindFirstObjectByType<ChronoLock.ChronoGame>();
        if(game && game.player){game.player.transform.SetPositionAndRotation(new Vector3(0,0,-7),Quaternion.identity);EditorUtility.SetDirty(game.player.transform);}
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Could not save opening scene.");
        Debug.Log("CHRONO_OPENING_BUILT root="+RootName+" backup="+backup+" cradle="+CradlePath+"; puzzles beyond z=0 preserved.");
    }
    static GameObject Find(Scene scene,string name)
    {
        foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true))
            if(t.name==name)return t.gameObject;
        return null;
    }
    static void Architecture()
    {
        Box("Opening floor foundation",new Vector3(0,-.2f,-7),new Vector3(12,.4f,14),hull,true);
        Box("Opening ceiling",new Vector3(0,4.9f,-7),new Vector3(12,.2f,14),hull,true);
        Box("Opening back hull",new Vector3(0,2.4f,-14),new Vector3(12,4.8f,.25f),hull,true);
        Box("Opening left hull",new Vector3(-6,2.4f,-7),new Vector3(.25f,4.8f,14),hull,true);
        Box("Opening right rear hull",new Vector3(6,2.4f,-11.7f),new Vector3(.25f,4.8f,4.6f),hull,true);
        Box("Opening right front hull",new Vector3(6,2.4f,-1.8f),new Vector3(.25f,4.8f,3.6f),hull,true);
        Box("Opening window sill hull",new Vector3(6,.65f,-6.5f),new Vector3(.25f,1.3f,5.4f),hull,true);
        Box("Opening window header hull",new Vector3(6,4.45f,-6.5f),new Vector3(.25f,.7f,5.4f),hull,true);
        // Replace only the original entrance wall. Center passage is four metres wide.
        Box("Opening exit left jamb",new Vector3(-4,2.4f,-.2f),new Vector3(4,4.8f,.4f),ceramic,true);
        Box("Opening exit right jamb",new Vector3(4,2.4f,-.2f),new Vector3(4,4.8f,.4f),ceramic,true);
        Box("Opening exit lintel",new Vector3(0,4.3f,-.2f),new Vector3(4,1,.4f),metal,true);
        var door=Box("Opening exit door",new Vector3(0,1.9f,-.2f),new Vector3(4,3.8f,.22f),metal,true);
        var seam=Box("Opening door center seam",new Vector3(0,1.9f,-.325f),new Vector3(.045f,3.6f,.018f),teal);
        seam.SetParent(door,true);
        Label("RESCUE / 01",new Vector3(-1.53f,4.08f,-.44f),.15f,white);
        for(int z=-13;z<=-1;z+=2)for(int x=-5;x<=5;x+=2)
        {
            Box("Opening deck cassette",new Vector3(x,.012f,z),new Vector3(1.965f,.024f,1.965f),Mathf.Abs(x)==5?metal:deck);
            if(Mathf.Abs(x)==5)for(int k=-1;k<=1;k++)Box("Opening service vent",new Vector3(x+k*.23f,.028f,z),new Vector3(.055f,.014f,1.2f),hull);
        }
        for(int z=-12;z<=-4;z+=4)
        {
            Wall(new Vector3(-5.96f,0,z),-90);
            Box("Opening ceiling rib",new Vector3(0,4.7f,z),new Vector3(11.7f,.16f,.15f),metal);
            Box("Opening overhead diffuser",new Vector3(0,4.62f,z),new Vector3(2.8f,.035f,.13f),white);
        }
        Wall(new Vector3(5.96f,0,-12),90);
        for(int side=-1;side<=1;side+=2)for(int z=-7;z<=-1;z+=2)
            Box("Opening path light",new Vector3(side*1.65f,.035f,z),new Vector3(.075f,.018f,1.5f),teal);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Opening empty rescue unit base",new Vector3(side*4,.25f,-10),new Vector3(2.05f,.5f,4.3f),metal,true);
            var shroud=Primitive(PrimitiveType.Capsule,"Opening empty sealed rescue unit",new Vector3(side*4,1.05f,-10),new Vector3(1.75f,1.7f,1.75f),hull);
            shroud.rotation=Quaternion.Euler(90,0,0);
            Box("Opening empty unit seam",new Vector3(side*4,1.92f,-10),new Vector3(.035f,.025f,3.1f),ceramic);
            Label("STASIS / OFFLINE",new Vector3(side*4-.68f,1.8f,-7.83f),.066f,amber);
        }
    }
    static void InstallCradle(GameObject asset)
    {
        var anchor=Marker("Opening cradle",new Vector3(0,0,-10),Quaternion.identity);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(asset);o.transform.SetParent(anchor,false);
        ReplaceMaterials(o);
        var renderers=o.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new InvalidOperationException("Blender cradle has no renderer.");
        Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
        float scale=Mathf.Min(1,Mathf.Min(2.8f/Mathf.Max(.01f,b.size.x),4.8f/Mathf.Max(.01f,b.size.z)));
        o.transform.localScale*=scale;
        b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
        o.transform.position+=new Vector3(-b.center.x,-b.min.y,-10-b.center.z);
        foreach(var c in o.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        // Collider is kept below the wake camera and clear of the standing point.
        var col=anchor.gameObject.AddComponent<BoxCollider>();col.center=new Vector3(0,.4f,0);col.size=new Vector3(2.3f,.8f,3.9f);
        Box("Opening occupied bay glow",new Vector3(0,.08f,-10),new Vector3(2.6f,.04f,4.4f),teal);
    }
    static void Medical()
    {
        Box("Opening monitor support",new Vector3(1.75f,2.6f,-7.9f),new Vector3(.14f,3.8f,.14f),metal);
        Box("Opening status monitor housing",new Vector3(0,3.15f,-7.8f),new Vector3(3.55f,1.12f,.16f),metal);
        Box("Opening status monitor face",new Vector3(0,3.15f,-7.905f),new Vector3(3.3f,.9f,.028f),hull);
        // Face toward the lying player at z=-10.2, independent of the forward corridor sign.
        var title=Label("ARKA / RESCUE PROTOCOL",new Vector3(-1.48f,3.43f,-7.94f),.092f,teal);title.name="Opening status display";
        Label("LIFE SUPPORT : STABLE",new Vector3(-1.45f,3.04f,-7.95f),.067f,white);
        for(int i=0;i<9;i++)
        {
            float x=-1.25f+i*.16f;float h=i%3==0?.21f:.065f;
            Box("Opening abstract medical glyph",new Vector3(x,2.86f,-7.94f),new Vector3(.055f,h,.016f),i%3==0?teal:white);
        }
        for(int side=-1;side<=1;side+=2)
        {
            Box("Opening cradle rail light",new Vector3(side*1.4f,.65f,-10),new Vector3(.045f,.035f,3.4f),teal);
            Box("Opening wall medical console",new Vector3(side*5.5f,1.55f,-4),new Vector3(.7f,.95f,1.3f),metal,true);
        }
    }
    static void Window()
    {
        Box("Opening black space viewport",new Vector3(6.45f,2.7f,-6.5f),new Vector3(.06f,2.7f,5.4f),sky);
        // Invisible collision surface retains a safe window; no title artwork is used.
        var barrier=Marker("Opening observation glass barrier",new Vector3(6,2.7f,-6.5f),Quaternion.identity);
        var collider=barrier.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.1f,2.8f,5.4f);
        for(int z=-1;z<=1;z+=2)Box("Opening viewport vertical frame",new Vector3(5.86f,2.7f,-6.5f+z*2.65f),new Vector3(.28f,2.9f,.14f),metal);
        for(int y=-1;y<=1;y+=2)Box("Opening viewport horizontal frame",new Vector3(5.86f,2.7f+y*1.38f,-6.5f),new Vector3(.28f,.12f,5.4f),metal);
        var horizon=Primitive(PrimitiveType.Cylinder,"Opening Nemesis event horizon",new Vector3(6.35f,2.75f,-6.5f),new Vector3(1.75f,.015f,1.75f),black);horizon.rotation=Quaternion.Euler(0,0,90);
        Ring("Opening accretion inner",new Vector3(6.31f,2.75f,-6.5f),1.00f,.07f,amber);
        Ring("Opening accretion outer",new Vector3(6.32f,2.75f,-6.5f),1.14f,.025f,white);
        for(int i=0;i<19;i++)
        {float yy=1.55f+(i*73%210)*.01f,zz=-8.9f+(i*97%480)*.01f;Box("Opening distant star",new Vector3(6.39f,yy,zz),new Vector3(.018f,.012f,.012f),white);}
    }
    static void Lighting()
    {
        LightAt("Opening cradle teal light",new Vector3(0,2.15f,-10),new Color(.20f,.85f,.8f),2.3f,5);
        LightAt("Opening ceiling soft fill",new Vector3(0,4.3f,-5),new Color(.52f,.7f,.78f),2.7f,11);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Opening emergency amber lamp",new Vector3(side*5.65f,3.5f,-11.8f),new Vector3(.09f,.11f,.55f),amber);
            LightAt("Opening emergency amber fill",new Vector3(side*4.7f,3.3f,-11.4f),new Color(1,.36f,.08f),1.25f,5);
        }
    }
    static Transform Marker(string name,Vector3 position,Quaternion rotation)
    {var o=new GameObject(name).transform;o.SetParent(root,false);o.position=position;o.rotation=rotation;return o;}
    static Transform Primitive(PrimitiveType type,string name,Vector3 position,Vector3 size,Material material,bool solid=false)
    {
        var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(root,false);o.transform.position=position;o.transform.localScale=size;
        o.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());return o.transform;
    }
    static Transform Box(string name,Vector3 p,Vector3 size,Material m,bool solid=false)=>Primitive(PrimitiveType.Cube,name,p,size,m,solid);
    static Transform Label(string text,Vector3 p,float size,Material m)
    {
        var t=Marker("Opening label / "+text,p,Quaternion.identity);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=size*.3f;label.anchor=TextAnchor.UpperLeft;label.color=m.GetColor("_BaseColor");
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var textMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChronoLock/Materials/WorldText.mat");
        if(textMaterial)t.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;
        return t;
    }
    static void LightAt(string name,Vector3 p,Color c,float intensity,float range)
    {var l=Marker(name,p,Quaternion.identity).gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;}
    static void Wall(Vector3 p,float yaw)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);if(!asset)return;
        var a=Marker("Opening reused beveled wall",p,Quaternion.Euler(0,yaw+180,0));a.localScale=new Vector3(1,.78f,1);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(asset);o.transform.SetParent(a,false);ReplaceMaterials(o);
        foreach(var c in o.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
    }
    static void ReplaceMaterials(GameObject instance)
    {
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                string n=mats[i]?mats[i].name.ToLowerInvariant():"";
                mats[i]=n.Contains("ceramic")?ceramic:n.Contains("teal")||n.Contains("glow")||n.Contains("sigil")?teal:n.Contains("amber")?amber:n.Contains("metal")?metal:n.Contains("soft")||n.Contains("glove")||n.Contains("cushion")?soft:hull;
            }
            renderer.sharedMaterials=mats;
        }
    }
    static Material Mat(string name,Color c,bool unlit,float metallic=0)
    {
        string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
        if(!shader)throw new InvalidOperationException("URP shader unavailable.");if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;m.SetColor("_BaseColor",c);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.45f);m.SetFloat("_Cull",0);EditorUtility.SetDirty(m);return m;
    }
    static void Ring(string name,Vector3 center,float radius,float width,Material m)
    {
        const int n=96;var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<=n;i++){float a=i*2*Mathf.PI/n;foreach(float r in new[]{radius-width*.5f,radius+width*.5f})vertices.Add(new Vector3(0,Mathf.Sin(a)*r,Mathf.Cos(a)*r));}
        for(int i=0;i<n;i++){int k=i*2;triangles.AddRange(new[]{k,k+1,k+3,k,k+3,k+2});}
        string path=Folder+name.Replace(" ","_")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var o=Marker(name,center,Quaternion.identity).gameObject;o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=m;
    }
}





