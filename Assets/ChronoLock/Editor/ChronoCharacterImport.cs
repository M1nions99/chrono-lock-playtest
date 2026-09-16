using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Character-only import and preview. Never opens or saves ChronoLab.unity.
public static class ChronoCharacterImport
{
    const string ModelPath="Assets/ChronoLock/Art/Character/Protagonist.fbx";
    const string MaterialFolder="Assets/ChronoLock/Materials/Character/";
    const string PrefabPath="Assets/ChronoLock/Resources/Protagonist.prefab";
    const string PreviewPath="Assets/ChronoLock/Scenes/ChronoCharacterPreview.unity";
    const float HumanHeight=1.78f;
    const int PreviewLayer=31;
    static readonly string[] Names={"Skin","SuitIvory","SuitGraphite","SuitOrange","Hair","Eyes","Glyph"};
    static readonly Color[] Colors={new Color(.61f,.39f,.29f),new Color(.74f,.76f,.71f),new Color(.065f,.082f,.094f),new Color(.92f,.25f,.055f),new Color(.035f,.025f,.022f),new Color(.76f,.81f,.78f),new Color(.12f,.84f,.78f)};

    [MenuItem("ChronoLock/Open Character Preview")]
    public static void OpenCharacterPreview()=>Build();

    // Batch: -executeMethod ChronoCharacterImport.Build; existing game scene stays untouched.
    public static void Build()
    {
        if(!File.Exists(ModelPath))throw new FileNotFoundException("주인공 FBX가 없습니다. 전신 모델을 먼저 "+ModelPath+" 에 배치하세요.",ModelPath);
        AssetDatabase.ImportAsset(ModelPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if(!importer)throw new InvalidOperationException("Protagonist.fbx is not a Unity model asset.");
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if(!source)throw new InvalidOperationException("Character model import failed.");
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(!shader)throw new InvalidOperationException("URP/Lit is required for character materials.");
        Directory.CreateDirectory(MaterialFolder);Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));AssetDatabase.Refresh();
        for(int sceneIndex=SceneManager.sceneCount-1;sceneIndex>=0;sceneIndex--){var loaded=SceneManager.GetSceneAt(sceneIndex);if(loaded.path!=PreviewPath)continue;if(loaded.isDirty)throw new InvalidOperationException("Character preview has unsaved edits; save or close it before rebuilding.");EditorSceneManager.CloseScene(loaded,true);}
        var previous=SceneManager.GetActiveScene();
        // A fresh batch process starts with an untitled bootstrap scene; Unity cannot add beside it.
        var mode=Application.isBatchMode&&string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive;
        var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
        GameObject actor=null;
        try
        {
            SceneManager.SetActiveScene(preview);
            actor=new GameObject("주인공");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,preview);
            // Keep the imported FBX root's axis correction and authored transforms intact.
            Vector3 pos=instance.transform.localPosition,scale=instance.transform.localScale;Quaternion rot=instance.transform.localRotation;
            instance.transform.SetParent(actor.transform,false);instance.transform.localPosition=pos;instance.transform.localRotation=rot;instance.transform.localScale=scale;
            int renderers=0,slots=0;
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                renderers++;var mats=renderer.sharedMaterials;
                if(mats.Length==0)throw new InvalidOperationException("Renderer has no material slots: "+renderer.name);
                for(int i=0;i<mats.Length;i++)
                {
                    if(!mats[i])throw new InvalidOperationException("Missing source material on "+renderer.name+" slot "+i);
                    string key=MaterialKey(mats[i].name);
                    if(key==null)throw new InvalidOperationException("Unmapped character material '"+mats[i].name+"'. Expected Skin/SuitIvory/SuitGraphite/SuitOrange/Hair/Eyes/Glyph.");
                    mats[i]=SourceMaterial(mats[i],key,shader);slots++;
                }
                renderer.sharedMaterials=mats;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                Mesh mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh || mesh.vertexCount==0)throw new InvalidOperationException("Empty character mesh: "+renderer.name);
                if(mats.Length<mesh.subMeshCount)throw new InvalidOperationException("Material count does not cover every submesh: "+renderer.name);
            }
            if(renderers==0)throw new InvalidOperationException("Character contains no renderable mesh.");
            var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool rigged=false;foreach(var skin in skins)if(skin.bones!=null&&skin.bones.Length>0){foreach(var bone in skin.bones)if(!bone)throw new InvalidOperationException("Missing rig bone on "+skin.name);rigged=true;}
            if(!rigged)throw new InvalidOperationException("Expected a rigged full-body character; no skinned renderer with bones was found.");
            Bounds before=BoundsOf(actor);
            if(!Finite(before.size.x)||!Finite(before.size.y)||!Finite(before.size.z)||before.size.y<.01f||before.size.y>1000)throw new InvalidOperationException("Invalid imported body height: "+before.size.y);
            actor.transform.localScale=Vector3.one*(HumanHeight/before.size.y);
            Bounds fitted=BoundsOf(actor);actor.transform.position=new Vector3(-fitted.center.x,-fitted.min.y,-fitted.center.z);
            Bounds final=BoundsOf(actor);
            if(!Finite(final.size.y)||!Finite(final.min.y)||Mathf.Abs(final.size.y-HumanHeight)>.006f||Mathf.Abs(final.min.y)>.006f)throw new InvalidOperationException("Character height/ground alignment verification failed: "+final);
            // Prefab remains on the normal gameplay layer. Preview isolation is applied afterward.
            var prefab=PrefabUtility.SaveAsPrefabAsset(actor,PrefabPath);
            if(!prefab)throw new IOException("Failed to save Resources/Protagonist prefab.");
            BuildStage(actor);
            EditorSceneManager.MarkSceneDirty(preview);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(preview,PreviewPath))throw new IOException("Failed to save character-only preview scene.");
            Selection.activeGameObject=actor;
            Debug.Log("CHRONO_CHARACTER_IMPORT_PASSED | name=주인공 | Generic rig | importedHeight="+before.size.y.ToString("F4")+"m | prefabHeight="+final.size.y.ToString("F4")+"m | renderers="+renderers+" materials="+slots+" | prefab="+PrefabPath+" | scene="+PreviewPath+" | source FBX root preserved; only wrapper scaled.");
        }
        catch
        {
            EditorSceneManager.CloseScene(preview,true);
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            throw;
        }
    }
    static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    static string MaterialKey(string name)
    {
        string n=name.Replace(" ","").Replace("_","").Replace("-","").ToLowerInvariant();
        foreach(var key in Names)if(n.Contains(key.ToLowerInvariant()))return key;
        if(n.Contains("eyebrow"))return "Hair";
        if(n.Contains("teeth")||n.Contains("tooth"))return "Skin";
        if(n.Contains("eye"))return "Eyes";
        return null;
    }
    static Material GetMaterial(string name,Color color,Shader shader)
    {
        string path=MaterialFolder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        // Existing authored materials/textures are deliberately preserved.
        if(!m)
        {
            m=new Material(shader);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",name.StartsWith("Suit")?.12f:0);m.SetFloat("_Smoothness",name=="Eyes"?.55f:name=="Skin"?.28f:.35f);
            if(name=="Glyph"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.45f);}
            AssetDatabase.CreateAsset(m,path);
        }
        return m;
    }
    static Material SourceMaterial(Material source,string category,Shader shader)
    {
        string guid;long localId;
        if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out localId))
            throw new InvalidOperationException("Cannot identify source material: "+source.name);
        string safe=source.name;foreach(char c in Path.GetInvalidFileNameChars())safe=safe.Replace(c,'_');
        string path=MaterialFolder+safe+"_"+guid+"_"+localId+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing)return existing; // Preserve all previously authored overrides.
        int index=Array.IndexOf(Names,category);
        Color color=index>=0?Colors[index]:Color.white;
        if(source.HasProperty("_BaseColor"))color=source.GetColor("_BaseColor");
        else if(source.HasProperty("_Color"))color=source.GetColor("_Color");
        var m=new Material(shader);m.name=source.name+" / URP";m.SetColor("_BaseColor",color);
        Texture texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null;
        string textureProperty="_BaseMap";
        if(!texture && source.HasProperty("_MainTex")){texture=source.GetTexture("_MainTex");textureProperty="_MainTex";}
        if(texture){m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",source.GetTextureScale(textureProperty));m.SetTextureOffset("_BaseMap",source.GetTextureOffset(textureProperty));}
        bool sourceCutout=source.IsKeywordEnabled("_ALPHATEST_ON")
            || (source.HasProperty("_Mode") && Mathf.RoundToInt(source.GetFloat("_Mode"))==1)
            || (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip")>.5f)
            || source.GetTag("RenderType",false,"")=="TransparentCutout";
        bool hairAlpha=false;
        if(category=="Hair" && texture)
        {
            var textureImporter=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if(textureImporter)hairAlpha=textureImporter.DoesSourceTextureHaveAlpha();
            else if(texture is Texture2D image)hairAlpha=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(image.graphicsFormat);
        }
        if(sourceCutout||hairAlpha)
        {
            // Standard exposes _Cutoff even for opaque materials; its mere presence is not clipping intent.
            float cutoff=sourceCutout && source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.3f;
            m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",Mathf.Clamp01(cutoff));m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=(int)RenderQueue.AlphaTest;
        }
        m.SetFloat("_Metallic",category.StartsWith("Suit")?.12f:0);
        m.SetFloat("_Smoothness",category=="Eyes"?.55f:category=="Skin"?.28f:.35f);
        if(category=="Glyph"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.45f);}
        ConfigureFaceSurface(m,source.name);
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static void ConfigureFaceSurface(Material material,string sourceName)
    {
        // The FBX diffuse channel cannot carry Blender's independent alpha-map link.
        // Restore the authored hair cards and transparent corneal shell explicitly.
        bool hair=sourceName.StartsWith("CL_CharacterHair",StringComparison.Ordinal);
        bool eyes=sourceName.StartsWith("CL_CharacterEyes",StringComparison.Ordinal);
        if(!hair&&!eyes)return;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChronoLock/Art/Character/Textures/"+(hair?"short04_diffuse.png":"brown_eye.png"));
        if(!texture)throw new FileNotFoundException("Required character face texture missing: "+sourceName);
        material.SetTexture("_BaseMap",texture);
        material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.3f);
        material.EnableKeyword("_ALPHATEST_ON");material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=(int)RenderQueue.AlphaTest;
        material.SetFloat("_Smoothness",hair?.12f:.3f);
        if(hair)
        {
            material.SetColor("_BaseColor",new Color(.10f,.075f,.055f));
            material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }
    [MenuItem("ChronoLock/Character/Restore Face Material Defaults")]
    public static void RestoreFaceMaterialDefaults()
    {
        // Explicit repair command: regular imports keep any artist's material overrides.
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{MaterialFolder.TrimEnd('/')}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            ConfigureFaceSurface(material,material.name);EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
    static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>(true);bool first=true;Bounds result=new Bounds();
        foreach(var r in renderers){if(!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer))continue;if(first){result=r.bounds;first=false;}else result.Encapsulate(r.bounds);}
        if(first)throw new InvalidOperationException("No character bounds.");return result;
    }
    static void BuildStage(GameObject actor)
    {
        foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=PreviewLayer;
        var stage=new GameObject("Character Preview / Studio");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Neutral shadow floor";floor.layer=PreviewLayer;floor.transform.SetParent(stage.transform,false);floor.transform.localScale=Vector3.one*.65f;
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        var floorMat=GetMaterial("PreviewFloor",new Color(.20f,.23f,.26f),Shader.Find("Universal Render Pipeline/Lit"));floor.GetComponent<Renderer>().sharedMaterial=floorMat;
        var camera=new GameObject("Character Preview Camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);camera.transform.position=new Vector3(2.35f,1.5f,3.6f);camera.transform.LookAt(new Vector3(0,.92f,0));camera.fieldOfView=32;camera.nearClipPlane=.05f;camera.farClipPlane=30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.18f);camera.cullingMask=1<<PreviewLayer;camera.allowHDR=true;camera.depth=100;
        var key=AddLight(stage.transform,"Large soft key",LightType.Directional,new Vector3(-3,4,4),new Color(1,.94f,.86f),2.2f);key.transform.LookAt(new Vector3(0,.9f,0));key.shadows=LightShadows.Soft;key.shadowBias=.035f;key.shadowNormalBias=.18f;
        var fill=AddLight(stage.transform,"Cool fill",LightType.Point,new Vector3(2,2,2),new Color(.68f,.8f,1),2.1f);fill.range=7;
        var rim=AddLight(stage.transform,"Shoulder rim",LightType.Spot,new Vector3(-1,2.8f,-2),new Color(.64f,.89f,1),4);rim.range=7;rim.spotAngle=62;rim.transform.LookAt(new Vector3(0,1.1f,0));
    }
    static Light AddLight(Transform parent,string name,LightType type,Vector3 position,Color color,float intensity)
    {var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=position;var light=o.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.cullingMask=1<<PreviewLayer;return light;}

    [MenuItem("ChronoLock/Capture Character Preview")]
    public static void CapturePreview()
    {
        if(!File.Exists(PreviewPath))throw new FileNotFoundException("Build the character preview before capturing it.",PreviewPath);
        string output=Path.GetFullPath("Builds/CharacterPreview");var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]=="--chrono-character-out")output=Path.GetFullPath(args[i+1]);
        Directory.CreateDirectory(output);
        Scene previous=SceneManager.GetActiveScene(),preview=SceneManager.GetSceneByPath(PreviewPath);
        bool opened=!preview.IsValid()||!preview.isLoaded;
        if(opened)preview=EditorSceneManager.OpenScene(PreviewPath,Application.isBatchMode&&string.IsNullOrEmpty(SceneManager.GetActiveScene().path)?OpenSceneMode.Single:OpenSceneMode.Additive);
        Camera camera=null;
        foreach(var go in preview.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Camera>(true))if(c.name=="Character Preview Camera")camera=c;
        if(!camera){if(opened)EditorSceneManager.CloseScene(preview,true);throw new InvalidOperationException("Named character preview camera missing.");}
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float oldAspect=camera.aspect;
        var rt=new RenderTexture(1200,1400,24);Texture2D pixels=null;
        // Exclude other loaded scenes' lights during captures, then restore their masks.
        var masks=new Dictionary<Light,int>();
        try
        {
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.gameObject.scene!=preview){masks[light]=light.cullingMask;light.cullingMask&=~(1<<PreviewLayer);}
            camera.targetTexture=rt;camera.aspect=1200f/1400;
            string[] names={"01-three-quarter.png","02-front.png","03-back.png"};
            Vector3[] poses={new Vector3(2.35f,1.5f,3.6f),new Vector3(0,1.25f,4.25f),new Vector3(0,1.25f,-4.25f)};
            for(int i=0;i<names.Length;i++)
            {
                camera.transform.position=poses[i];camera.transform.LookAt(new Vector3(0,.92f,0));camera.Render();RenderTexture.active=rt;
                pixels=new Texture2D(1200,1400,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,1400),0,0);pixels.Apply();
                string path=Path.Combine(output,names[i]);File.WriteAllBytes(path,pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pixels);pixels=null;
                if(!File.Exists(path)||new FileInfo(path).Length==0)throw new IOException("Character capture empty: "+path);
                Debug.Log("CHRONO_CHARACTER_PREVIEW_CAPTURED "+path);
            }
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;
            foreach(var pair in masks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            if(opened)EditorSceneManager.CloseScene(preview,true);
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
        }
    }
}

