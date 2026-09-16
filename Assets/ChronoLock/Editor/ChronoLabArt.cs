using System.Collections.Generic;
using System.IO;
using ChronoLock;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ChronoLabArt
{
    const string Assets = "Assets/ChronoLock/Art/Polished/";
    const string Materials = "Assets/ChronoLock/Materials/";
    static Transform art;
    static Material ceramic, metal, dark, teal, amber, floor, light, space;

    public static void Upgrade(ChronoGame game)
    {
        Directory.CreateDirectory(Assets); Directory.CreateDirectory(Materials);
        art = new GameObject("LAB 07 / Architectural finish").transform; art.SetParent(game.transform);
        ceramic = Mat("CL_Ceramic", new Color(.70f,.78f,.80f), .18f, .48f);
        metal = Mat("CL_Metal", new Color(.16f,.23f,.28f), .78f, .58f);
        dark = Mat("CL_Dark", new Color(.025f,.045f,.065f), .45f, .45f);
        teal = Mat("CL_Teal", new Color(.05f,.7f,.76f), .2f, .5f, 1.2f);
        amber = Mat("CL_Amber", new Color(1,.41f,.12f), .1f, .45f, .65f);
        floor = Mat("CL_Deck", new Color(.23f,.29f,.33f), .55f, .45f);
        light = Mat("CL_Diffuser", new Color(.82f,.91f,1), .05f,.4f,2);
        space = Mat("CL_Space", new Color(.001f,.003f,.009f),0,0);
        string[] hidden = {"Left hull","Right hull","Left inset","Right inset","Ceiling rib","Left guide","Right guide","Deck seam","Ceiling light","Gap warning left","Gap warning right"};
        foreach (var renderer in game.GetComponentsInChildren<Renderer>())
            foreach (string name in hidden) if (renderer.name == name) renderer.enabled = false;
        foreach (var oldLight in game.GetComponentsInChildren<Light>()) oldLight.enabled = false;
        var ceiling = GameObject.Find("Ceiling"); if (ceiling) ceiling.GetComponent<Renderer>().sharedMaterial = dark;
        Architecture(); Observation(); Rotor(game); Devices(game);
        Door(game.powerDoor, amber); Door(game.exitDoor, teal); Lighting(game);
        AssetDatabase.SaveAssets();
    }

    static void Architecture()
    {
        for (int z=2; z<52; z+=4)
        {
            Model("WallModule",new Vector3(-5.97f,0,z),-90,art);
            if (z<14 || z>22) Model("WallModule",new Vector3(5.97f,0,z),90,art);
            for (int side=-1;side<=1;side+=2)
            {
                Box("Upper service fascia",new Vector3(side*5.88f,5.03f,z),new Vector3(.18f,.88f,3.86f),dark);
                Box("Upper ceramic band",new Vector3(side*5.72f,4.65f,z),new Vector3(.17f,.11f,3.8f),ceramic);
                Box("Recessed running light",new Vector3(side*5.61f,4.73f,z),new Vector3(.025f,.045f,3.3f),light);
                Beam("Angled ceiling bracket",new Vector3(side*5.73f,4.94f,z-1.8f),new Vector3(side*4.65f,5.88f,z-1.8f),.14f,metal);
                Box("Ceiling side service tray",new Vector3(side*4.67f,5.75f,z),new Vector3(.6f,.13f,3.83f),metal);
                Box("Ceiling tray inset",new Vector3(side*4.67f,5.665f,z),new Vector3(.38f,.025f,3.46f),dark);
                if (z<32 || z>38)
                    Box("Deck perimeter light",new Vector3(side*5.43f,.042f,z),new Vector3(.035f,.015f,3.55f),light);
            }
            Box("Ceiling bridge structure",new Vector3(0,5.92f,z-1.85f),new Vector3(9.4f,.16f,.2f),metal);
            Box("Ceiling central panel",new Vector3(0,6.0f,z),new Vector3(8.8f,.07f,3.62f),floor);
            for(int x=-3;x<=3;x+=6)
            {
                Box("Luminaire housing",new Vector3(x,5.83f,z),new Vector3(.56f,.11f,2.4f),dark);
                Box("Luminaire diffuser",new Vector3(x,5.757f,z),new Vector3(.34f,.025f,2.17f),light);
            }
        }
        for(int z=1;z<52;z+=2)
        {
            if(z>=32 && z<38) continue;
            for(int x=-5;x<=5;x+=2)
            {
                Box("Deck cassette",new Vector3(x,.012f,z),new Vector3(1.976f,.024f,1.976f),Mathf.Abs(x)==5?metal:floor);
                if(Mathf.Abs(x)==5)
                    for(int k=0;k<3;k++) Box("Edge drain slots",new Vector3(x-.25f+k*.25f,.026f,z),new Vector3(.045f,.007f,1.62f),dark);
            }
        }
        foreach(float z in new[]{8.4f,11.6f,26.4f,31.7f,38.3f,47.4f})
        {
            Box("Threshold base",new Vector3(0,.038f,z),new Vector3(10.7f,.025f,.21f),dark);
            for(int i=-10;i<=10;i++)
            {
                var stripe=Box("Threshold warning marks",new Vector3(i*.49f,.056f,z),new Vector3(.19f,.008f,.17f),amber);
                stripe.localRotation=Quaternion.Euler(0,-24,0);
            }
        }
        for(int side=-1;side<=1;side+=2)
        {
            for(int z=33;z<=37;z+=2)
                Box("Gap side guard post",new Vector3(side*5.5f,.55f,z),new Vector3(.07f,1.1f,.07f),metal);
            Box("Gap side safety rail",new Vector3(side*5.5f,1.1f,35),new Vector3(.09f,.07f,5.7f),ceramic);
            for(int z=16;z<=24;z+=4)
            {
                Box("Research equipment cabinet",new Vector3(-5.12f,.83f,z),new Vector3(.75f,1.65f,1.6f),dark);
                Box("Cabinet face",new Vector3(-4.72f,.94f,z),new Vector3(.04f,1.35f,1.42f),ceramic);
                for(int k=0;k<5;k++) Box("Cabinet vent",new Vector3(-4.687f,.46f+k*.085f,z),new Vector3(.017f,.025f,1.04f),metal);
                Box("Cabinet readout",new Vector3(-4.68f,1.27f,z),new Vector3(.02f,.12f,.48f),teal);
            }
        }
    }

    static void Observation()
    {
        Box("Observation upper lintel",new Vector3(6,4.1f,18),new Vector3(.42f,.8f,12),ceramic);
        Box("Observation sill",new Vector3(5.92f,.75f,18),new Vector3(.6f,1.5f,12),dark);
        Box("Observation sill cap",new Vector3(5.8f,1.53f,18),new Vector3(.65f,.09f,12),metal);
        for(int z=12;z<=24;z+=4)
            Box("Observation mullion",new Vector3(5.91f,2.7f,z),new Vector3(.27f,2.5f,.16f),metal);
        Box("Observation sill light",new Vector3(5.43f,1.56f,18),new Vector3(.04f,.025f,11.75f),teal);
        Box("Deep space backdrop",new Vector3(39,12,18),new Vector3(.3f,65,130),space);
        var planet=Primitive(PrimitiveType.Sphere,"Distant glacial moon",new Vector3(29,9,27),new Vector3(15,15,15),Mat("CL_Planet",new Color(.07f,.17f,.23f),.15f,.2f));
        var random=new System.Random(71);
        for(int i=0;i<95;i++)
            Primitive(PrimitiveType.Sphere,"Distant star",new Vector3(37, (float)random.NextDouble()*35-8,(float)random.NextDouble()*90-25),Vector3.one*(.025f+(float)random.NextDouble()*.055f),light);
    }

    static void Rotor(ChronoGame game)
    {
        foreach(var renderer in game.rotor.movingPart.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
        Vector3 center=new Vector3(0,2.8f,10);
        MeshObject("Rotor structural ring",RingMesh("RotorOuterRing",2.63f,.14f),center,metal,art);
        MeshObject("Rotor inset ring",RingMesh("RotorInnerRing",2.45f,.034f),center+Vector3.back*.13f,light,art);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Rotor side mounting",new Vector3(side*3.52f,2.8f,10),new Vector3(1.52f,5.6f,.65f),dark);
            Box("Rotor mounting ceramic",new Vector3(side*3.52f,2.85f,9.64f),new Vector3(1.22f,4.96f,.045f),ceramic);
            for(int k=0;k<7;k++) Box("Rotor cooling slots",new Vector3(side*3.52f,1.45f+k*.3f,9.607f),new Vector3(.84f,.075f,.016f),metal);
        }
        Mesh blade=BladeMesh();
        for(int i=0;i<6;i++)
        {
            var b=MeshObject("Swept rotor blade",blade,center,metal,game.rotor.movingPart);
            b.localPosition=Vector3.zero;b.localRotation=Quaternion.Euler(0,0,i*60);
        }
        var hub=Primitive(PrimitiveType.Cylinder,"Rotor ceramic hub",center,new Vector3(.83f,.18f,.83f),ceramic,game.rotor.movingPart);
        hub.rotation=Quaternion.Euler(90,0,0);
        var cap=Primitive(PrimitiveType.Cylinder,"Rotor center indicator",center+Vector3.back*.2f,new Vector3(.36f,.018f,.36f),teal,game.rotor.movingPart);
        cap.rotation=Quaternion.Euler(90,0,0);
    }

    static void Devices(ChronoGame game)
    {
        ReplaceConsole("STOP / aim here",new Vector3(-2,0,7.3f),game.rotor.transform);
        ReplaceConsole("REWIND / aim here",new Vector3(-2.6f,0,30),game.bridge.transform);
        var old=GameObject.Find("Reactor console");
        if(old) { old.GetComponent<Renderer>().enabled=false;old.transform.position=new Vector3(-2.5f,1.75f,22);old.transform.localScale=new Vector3(2.4f,3.5f,2.4f); }
        Model("Reactor",new Vector3(-2.5f,0,22),0,game.reactor.transform);
        if(game.reactor.movingPart) game.reactor.movingPart.GetComponent<Renderer>().enabled=false;
        var deck=game.bridge.movingPart;
        deck.GetComponent<Renderer>().sharedMaterial=metal;
        for(int side=-1;side<=1;side+=2)
        {
            for(int z=-2;z<=2;z+=2)
                Box("Bridge rail post",deck.position+new Vector3(side*1.84f,.73f,z),new Vector3(.07f,1.05f,.07f),metal,deck);
            Box("Bridge ceramic handrail",deck.position+new Vector3(side*1.84f,1.25f,0),new Vector3(.12f,.08f,5.96f),ceramic,deck);
            Box("Bridge edge tracer",deck.position+new Vector3(side*1.94f,.23f,0),new Vector3(.025f,.025f,5.92f),teal,deck);
        }
        for(int z=-2;z<=2;z++) Box("Bridge deck plates",deck.position+new Vector3(0,.215f,z),new Vector3(3.55f,.025f,.94f),floor,deck);
    }

    static void ReplaceConsole(string name,Vector3 position,Transform parent)
    {
        var old=GameObject.Find(name);
        if(old) {old.GetComponent<Renderer>().enabled=false;old.transform.position=position+Vector3.up*.66f;old.transform.localScale=new Vector3(1,1.32f,.7f);}
        Model("Console",position,0,parent);
    }

    static void Door(Transform door,Material accent)
    {
        door.GetComponent<Renderer>().sharedMaterial=dark;
        float z=door.position.z;
        for(int side=-1;side<=1;side+=2)
        {
            Box("Split door ceramic leaf",new Vector3(side*.97f,2,z-.282f),new Vector3(1.88f,3.75f,.055f),ceramic,door);
            Box("Door lower inset",new Vector3(side*.97f,.54f,z-.322f),new Vector3(1.55f,.5f,.04f),metal,door);
            Box("Door central locking spine",new Vector3(side*.17f,2,z-.329f),new Vector3(.19f,2.77f,.028f),dark,door);
            Box("Door lock readout",new Vector3(side*.17f,2.25f,z-.351f),new Vector3(.055f,.55f,.02f),accent,door);
            Box("Bulkhead frame",new Vector3(side*2.27f,2.05f,z-.39f),new Vector3(.27f,4.25f,.14f),metal);
        }
        Box("Bulkhead header",new Vector3(0,4.22f,z-.4f),new Vector3(4.75f,.18f,.15f),ceramic);
    }

    static void Lighting(ChronoGame game)
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.20f,.26f,.32f);
        RenderSettings.ambientEquatorColor=new Color(.23f,.27f,.31f);RenderSettings.ambientGroundColor=new Color(.10f,.12f,.15f);
        RenderSettings.fogDensity=.003f;RenderSettings.fogColor=new Color(.055f,.08f,.11f);
        var sun=new GameObject("Station soft key");sun.transform.SetParent(art);sun.transform.rotation=Quaternion.Euler(47,-24,0);
        var key=sun.AddComponent<Light>();key.type=LightType.Directional;key.intensity=.55f;key.color=new Color(.78f,.88f,1);key.shadows=LightShadows.Soft;key.shadowStrength=.55f;
        for(int z=3;z<52;z+=6)
        {
            var go=new GameObject("Ceiling reflected fill");go.transform.SetParent(art);go.transform.position=new Vector3(0,4.5f,z);
            var l=go.AddComponent<Light>();l.type=LightType.Point;l.range=9;l.intensity=8.5f;l.color=new Color(.9f,.95f,1);l.shadows=LightShadows.None;
        }
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Assets+"LabVolume.asset");
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Assets+"LabVolume.asset");}
        if(!profile.TryGet<Bloom>(out var bloom)) {bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}
        bloom.intensity.Override(.17f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.65f);
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}
        tone.mode.Override(TonemappingMode.ACES);EditorUtility.SetDirty(profile);
        var volume=art.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        game.player.view.GetUniversalAdditionalCameraData().renderPostProcessing=true;
    }

    static Material Mat(string name,Color color,float metallic,float smooth,float glow=0)
    {
        string path=Materials+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find(glow>0?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
        m.DisableKeyword("_EMISSION");m.SetColor("_BaseColor",glow>0?color*Mathf.Max(1,glow):color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
        EditorUtility.SetDirty(m);return m;
    }
    static Transform Model(string name,Vector3 position,float yaw,Transform parent)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Assets+name+".fbx");if(!asset){Debug.LogWarning("Missing art "+name);return null;}
        var holder=new GameObject(name+" / art anchor").transform;holder.SetParent(parent,false);holder.position=position;holder.rotation=Quaternion.Euler(0,yaw+180,0);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(asset);o.transform.SetParent(holder,false);
        foreach(var r in o.GetComponentsInChildren<Renderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++){string n=mats[i]?mats[i].name:"";mats[i]=n.Contains("Ceramic")?ceramic:n.Contains("Teal")?teal:n.Contains("Amber")?amber:n.Contains("Metal")?metal:dark;}
            r.sharedMaterials=mats;
        }
        foreach(var c in o.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return holder;
    }
    static Transform Primitive(PrimitiveType type,string name,Vector3 p,Vector3 size,Material m,Transform parent=null)
    {
        var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.position=p;o.transform.localScale=size;o.transform.SetParent(parent?parent:art,true);
        o.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(o.GetComponent<Collider>());return o.transform;
    }
    static Transform Box(string n,Vector3 p,Vector3 s,Material m,Transform parent=null)=>Primitive(PrimitiveType.Cube,n,p,s,m,parent);
    static void Beam(string n,Vector3 a,Vector3 b,float width,Material m)
    {var o=Box(n,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),m);o.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static Transform MeshObject(string n,Mesh mesh,Vector3 p,Material m,Transform parent)
    {var o=new GameObject(n);o.transform.position=p;o.transform.SetParent(parent,true);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=m;return o.transform;}
    static Mesh SaveMesh(string name,List<Vector3> v,List<int> triangles)
    {
        string path=Assets+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.name=name;mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh RingMesh(string name,float radius,float tube)
    {
        var v=new List<Vector3>();var t=new List<int>();const int n=72,k=8;
        for(int i=0;i<n;i++)for(int j=0;j<k;j++){float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/k;v.Add(new Vector3(Mathf.Cos(a)*(radius+tube*Mathf.Cos(b)),Mathf.Sin(a)*(radius+tube*Mathf.Cos(b)),tube*Mathf.Sin(b)));}
        for(int i=0;i<n;i++)for(int j=0;j<k;j++){int a=i*k+j,b=((i+1)%n)*k+j,c=((i+1)%n)*k+(j+1)%k,d=i*k+(j+1)%k;t.AddRange(new[]{a,b,c,a,c,d});}
        return SaveMesh(name,v,t);
    }
    static Mesh BladeMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<=8;i++)
        {float f=i/8f,r=.38f+f*1.92f,a=f*f*.47f,w=.1f+Mathf.Sin(f*Mathf.PI*.8f)*.25f;Vector3 center=new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r,0),right=new Vector3(Mathf.Cos(a),-Mathf.Sin(a),0);v.Add(center-right*w+Vector3.back*.07f);v.Add(center+right*w+Vector3.back*.07f);v.Add(center+right*w+Vector3.forward*.07f);v.Add(center-right*w+Vector3.forward*.07f);}
        for(int i=0;i<8;i++)for(int j=0;j<4;j++){int a=i*4+j,b=i*4+(j+1)%4,c=b+4,d=a+4;t.AddRange(new[]{a,b,c,a,c,d});}
        t.AddRange(new[]{0,2,1,0,3,2,32,33,34,32,34,35});return SaveMesh("SweptRotorBlade",v,t);
    }
}
