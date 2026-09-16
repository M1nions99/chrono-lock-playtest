using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoLock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Batch-only production API smoke test. No physical mouse/keyboard simulation.
[InitializeOnLoad]
public static class ChronoFirstPersonCheck
{
    const string Key="ChronoLock.ArmQA.Running",OutKey="ChronoLock.ArmQA.Output",SettingsKey="ChronoLock.ArmQA.Settings";
    static ChronoGame game;static ChronoWrist wrist;
    static int stage,frame=-1;static double started,at;static bool finishing;
    static Vector3 fixedPosition,fixedScale;static Quaternion fixedRotation;static Color freeze,accelerate;
    static readonly List<string> checks=new List<string>(),errors=new List<string>();
    static string Output=>SessionState.GetString(OutKey,Path.GetFullPath("Builds/FirstPersonQA"));
    static ChronoFirstPersonCheck(){EditorApplication.update+=Update;Application.logMessageReceived+=OnLog;}
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Arm QA exits Unity automatically. Run in a separate batch Editor only.");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start Arm QA from Edit Mode.");
        string output=Path.GetFullPath("Builds/FirstPersonQA");var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]=="--chrono-arm-out")output=Path.GetFullPath(args[i+1]);
        Directory.CreateDirectory(output);SessionState.SetString(OutKey,output);SessionState.SetString(SettingsKey,"");
        EditorSceneManager.OpenScene("Assets/ChronoLock/Scenes/ChronoLab.unity");
        game=null;wrist=null;stage=0;frame=-1;started=at=0;finishing=false;checks.Clear();errors.Clear();
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void OnLog(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool(Key,false)||finishing)return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase"))return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||finishing)return;
        try
        {
            if(started==0)started=at=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-started>65)throw new Exception("Arm QA timed out at "+stage);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||frame==Time.frameCount)return;frame=Time.frameCount;
            if(errors.Count>0)throw new Exception(errors[0]);
            if(!game)
            {
                var found=UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
                if(!found||!found.player||!found.Interface||Time.frameCount<5)return;
                var hands=found.player.view.GetComponent<ChronoWrist>();if(!hands||!hands.Presentation||!hands.ArmCamera){if(EditorApplication.timeSinceStartup-started>10)throw new Exception("ChronoWrist did not initialize its presentation and overlay camera.");return;}
                game=found;wrist=hands;SessionState.SetString(SettingsKey,JsonUtility.ToJson(ChronoPreferences.Current));
                game.player.enabled=false;var initial=ChronoPreferences.Current;initial.fov=78;initial.cameraMotion=true;ChronoPreferences.Apply(initial);game.player.view.fieldOfView=78;at=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-at<.4)return;
            switch(stage)
            {
                case 0:
                    Check(game.Interface.Page==ChronoPage.Title&&!wrist.IsVisible,"Boot title hides right arm");
                    Check(UnityEngine.Object.FindObjectsByType<ChronoWrist>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Exactly one ChronoWrist");
                    int cameras=0;foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(c.name=="First person arm camera")cameras++;
                    Check(cameras==1,"Exactly one arm overlay camera");
                    int stackCount=0;foreach(var c in game.player.view.GetUniversalAdditionalCameraData().cameraStack)if(c==wrist.ArmCamera)stackCount++;
                    Check(stackCount==1&&wrist.ArmCamera.GetUniversalAdditionalCameraData().renderType==CameraRenderType.Overlay,"Arm camera occurs once in production URP stack");
                    bool legacy=false;foreach(var t in Resources.FindObjectsOfTypeAll<Transform>())if(t.gameObject.scene.IsValid()&&(t.name=="Temporal wrist instrument"||t.name=="PalmAnchorPresentation"))legacy=true;Check(!legacy,"Legacy wrist and palm objects absent, including inactive objects");
                    Check(wrist.Presentation.GetComponentsInChildren<Collider>(true).Length==0,"Arm hierarchy has no colliders");
                    Meshes();game.Restart();break;
                case 1:
                    Check(wrist.IsVisible&&wrist.ArmCamera.enabled,"Restart shows arm");BoundsCheck();Capture("01-rest-fov78.png");
                    Check(game.ApplyToTarget(game.rotor,TemporalMode.Freeze),"Production rotor freeze accepted");break;
                case 2:
                    Check(wrist.IsVisible&&game.SelectedMode==TemporalMode.Freeze,"Freeze presentation visible");freeze=wrist.GlyphColor;GlyphApplied();
                    Check(freeze.b>freeze.r&&freeze.g>freeze.r,"Freeze glyph is cyan");Capture("02-freeze.png");
                    Check(game.ApplyToTarget(game.reactor,TemporalMode.Accelerate),"Production reactor acceleration accepted");break;
                case 3:
                    accelerate=wrist.GlyphColor;GlyphApplied();Check(Difference(accelerate,freeze)>.3f&&accelerate.r>accelerate.b,"Acceleration glyph differs and is orange");Capture("03-accelerate.png");
                    Check(game.ApplyToTarget(game.bridge,TemporalMode.Rewind),"Production bridge rewind accepted");break;
                case 4:
                    var rewind=wrist.GlyphColor;GlyphApplied();Check(Difference(rewind,freeze)>.3f&&Difference(rewind,accelerate)>.3f&&rewind.b>rewind.g,"Rewind glyph differs and is violet");Capture("04-rewind.png");game.Restart();break;
                case 5:
                    Check(wrist.IsVisible&&game.ActiveDevice==null&&game.SelectedMode==TemporalMode.Freeze,"Restart restores idle arm without active device");Capture("05-reset-rest.png");game.Pause();break;
                case 6:
                    Check(!wrist.IsVisible&&!wrist.ArmCamera.enabled,"Pause hides arm and overlay");game.ReturnToTitle();break;
                case 7:
                    Check(!wrist.IsVisible&&game.Interface.Page==ChronoPage.Title,"Return title hides arm");game.Resume();break;
                case 8:
                    Check(wrist.IsVisible,"Resume shows arm");Fov(60);break;
                case 9:
                    BoundsCheck();Capture("06-fov60-motion-off.png");fixedPosition=wrist.Presentation.localPosition;fixedRotation=wrist.Presentation.localRotation;fixedScale=wrist.Presentation.localScale;break;
                case 10:
                    Check(Vector3.Distance(fixedPosition,wrist.Presentation.localPosition)<.00001f&&Quaternion.Angle(fixedRotation,wrist.Presentation.localRotation)<.001f&&Vector3.Distance(fixedScale,wrist.Presentation.localScale)<.00001f,"Motion-off pose stable for at least .4 seconds");Fov(78);break;
                case 11:BoundsCheck();Capture("07-fov78-motion-off.png");Fov(100);break;
                case 12:BoundsCheck();Capture("08-fov100-motion-off.png");Finish(true,"CHRONO_FIRST_PERSON_QA_PASSED");return;
            }
            stage++;at=EditorApplication.timeSinceStartup;
        }
        catch(Exception ex){Finish(false,ex.ToString());}
    }
    static void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);Debug.Log("ARM_PASS "+text);}
    static float Difference(Color a,Color b)=>Vector3.Distance(new Vector3(a.r,a.g,a.b),new Vector3(b.r,b.g,b.b));
    static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    static void GlyphApplied()
    {
        bool found=false;var block=new MaterialPropertyBlock();
        foreach(var renderer in wrist.Presentation.GetComponentsInChildren<Renderer>())
        {
            bool glyph=false;foreach(var material in renderer.sharedMaterials)if(material&&material.name.Contains("Glyph"))glyph=true;
            if(!glyph)continue;found=true;renderer.GetPropertyBlock(block);
            Check(Difference(block.GetColor("_BaseColor"),wrist.GlyphColor)<.01f,"Glyph renderer property block matches selected color");
        }
        Check(found,"Visible arm includes a Glyph material renderer");
    }
    static void Meshes()
    {
        int vertices=0,triangles=0,renderers=0;
        foreach(var renderer in wrist.Presentation.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {Check(renderer.sharedMesh&&renderer.sharedMesh.vertexCount>0,"Arm skin mesh has vertices: "+renderer.name);vertices+=renderer.sharedMesh.vertexCount;for(int i=0;i<renderer.sharedMesh.subMeshCount;i++)triangles+=(int)renderer.sharedMesh.GetIndexCount(i)/3;renderers++;}
        Check(renderers>0&&triangles>0,"Arm mesh totals: renderers="+renderers+" vertices="+vertices+" triangles="+triangles);
    }
    static void Fov(float fov)
    {var data=ChronoPreferences.Current;data.fov=fov;data.cameraMotion=false;ChronoPreferences.Apply(data);game.player.view.fieldOfView=fov;}
    static void BoundsCheck()
    {
        Check(wrist.IsVisible&&Finite(wrist.Presentation.localPosition)&&Finite(wrist.Presentation.localScale),"Finite visible arm transform at FOV "+game.player.view.fieldOfView);
        Check(Mathf.Abs(wrist.ArmCamera.fieldOfView-game.player.view.fieldOfView)<.01f,"Arm overlay matches gameplay FOV");
        bool visible=false;var planes=GeometryUtility.CalculateFrustumPlanes(wrist.ArmCamera);
        foreach(var r in wrist.Presentation.GetComponentsInChildren<SkinnedMeshRenderer>())
        {Check(Finite(r.bounds.center)&&Finite(r.bounds.size)&&r.bounds.size.sqrMagnitude>0,"Finite arm renderer bounds: "+r.name);visible|=GeometryUtility.TestPlanesAABB(planes,r.bounds);}
        Check(visible,"At least one arm renderer intersects overlay frustum");
    }
    static void Capture(string name)
    {
        var camera=game.player.view;var canvas=game.Interface.RuntimeCanvas;var texture=new RenderTexture(1280,720,24);
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
        float oldNear=camera.nearClipPlane,oldPlane=canvas.planeDistance,oldAspect=camera.aspect;
        bool oldHDR=camera.allowHDR;var urp=camera.GetComponent<UniversalAdditionalCameraData>();bool oldPost=urp&&urp.renderPostProcessing;
        var scaler=canvas.GetComponent<CanvasScaler>();var handle=typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
        Texture2D pixels=null;
        try
        {
            camera.targetTexture=texture;camera.aspect=1280f/720;camera.nearClipPlane=.01f;camera.allowHDR=false;if(urp)urp.renderPostProcessing=false;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.05f;
            Canvas.ForceUpdateCanvases();if(scaler&&handle!=null)handle.Invoke(scaler,null);Canvas.ForceUpdateCanvases();
            camera.Render();RenderTexture.active=texture;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;
            camera.targetTexture=oldTarget;camera.aspect=oldAspect;camera.nearClipPlane=oldNear;camera.allowHDR=oldHDR;if(urp)urp.renderPostProcessing=oldPost;RenderTexture.active=oldActive;
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);texture.Release();UnityEngine.Object.DestroyImmediate(texture);
            Canvas.ForceUpdateCanvases();if(scaler&&handle!=null)handle.Invoke(scaler,null);Canvas.ForceUpdateCanvases();
        }
    }

    static void Finish(bool success,string message)
    {
        if(finishing)return;finishing=true;SessionState.SetBool(Key,false);
        try
        {
            string backup=SessionState.GetString(SettingsKey,"");if(!string.IsNullOrEmpty(backup))ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(backup));
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"first-person-result.txt"),message+"\nProduction gameplay interaction APIs and real-time ChronoWrist updates. Player input disabled: FOV tests apply settings and explicitly set gameplay Camera.fieldOfView; this does not validate ChronoPlayer input/FOV interpolation. Motion-off pose observed for >=.4 seconds. No hardware input injection or full escape route. 1280x720 captures use production base camera/URP stack with temporary HUD Canvas camera mode and capture-only HDR/post changes. Frustum/bounds checks are geometric; visual arm framing requires screenshot review. Saved preferences are not modified.\n"+string.Join("\n",checks)+"\n"+string.Join("\n",errors));
        }
        catch(Exception ex){Debug.LogError(ex);success=false;}
        if(success)Debug.Log(message);else Debug.LogError("CHRONO_FIRST_PERSON_QA_FAILED "+message);
        EditorApplication.Exit(success?0:1);
    }
}


