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

// Real-time opening/state API smoke test. No synthetic clock or hardware input injection.
[InitializeOnLoad]
public static class ChronoOpeningCheck
{
    const string Key="ChronoLock.OpeningQA.Running", OutKey="ChronoLock.OpeningQA.Output", SettingsKey="ChronoLock.OpeningQA.Settings";
    static ChronoGame game;
    static int stage, frame=-1;
    static double started, at;
    static float openingTime, puzzleTime;
    static Vector3 fixedPosition;
    static Quaternion fixedRotation;
    static bool finishing;
    static readonly List<string> checks=new List<string>(), errors=new List<string>();
    static string Output=>SessionState.GetString(OutKey,Path.GetFullPath("Builds/OpeningQA"));
    static ChronoOpeningCheck(){EditorApplication.update+=Update;Application.logMessageReceived+=OnLog;}
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run opening QA from Edit Mode.");
        string output=Path.GetFullPath("Builds/OpeningQA");var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]=="--chrono-opening-out")output=Path.GetFullPath(args[i+1]);
        Directory.CreateDirectory(output);SessionState.SetString(OutKey,output);
        EditorSceneManager.OpenScene("Assets/ChronoLock/Scenes/ChronoLab.unity");
        game=null;stage=0;frame=-1;started=at=0;finishing=false;checks.Clear();errors.Clear();
        SessionState.SetString(SettingsKey,"");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void OnLog(string text,string stack,LogType type)
    {
        if(!SessionState.GetBool(Key,false)||finishing)return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase"))return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||finishing)return;
        try
        {
            if(started==0)started=at=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-started>75)throw new Exception("Opening QA timeout at stage "+stage);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||frame==Time.frameCount)return;
            frame=Time.frameCount;
            if(errors.Count>0)throw new Exception(errors[0]);
            if(!game)
            {
                game=UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
                if(!game||!game.Interface||!game.Opening||Time.frameCount<4){game=null;return;}
                SessionState.SetString(SettingsKey,JsonUtility.ToJson(ChronoPreferences.Current));
                Check(game.Opening.HasRoom,"Physical opening room poses exist");
                Check(Resources.Load<GameObject>("ProtagonistRightArm"),"Protagonist right arm resource is imported");
                Check(!game.HasStarted&&game.Paused,"Story boots at title before gameplay");
                // Keep the runtime Opening component enabled; only autonomous player input is disabled.
                game.player.enabled=false;
                var settings=ChronoPreferences.Current;settings.cameraMotion=true;ChronoPreferences.Apply(settings);
                game.Interface.StartStory();
                Check(game.Opening.Running&&game.HasStarted&&!game.IsPlaying,"Story starts physical awakening instead of text page");
                Check(game.Interface.Page==ChronoPage.Awakening,"Awakening UI active");
                puzzleTime=game.elapsed;at=EditorApplication.timeSinceStartup;return;
            }
            var opening=game.Opening;var ui=game.Interface;
            switch(stage)
            {
                case 0:
                    FrozenPuzzle();
                    if(opening.Elapsed<1.7f)break;
                    Capture("01-waking-in-bed.png");
                    Check(opening.Phase==0,"Eye-opening phase precedes palm reveal");
                    Next();break;
                case 1:
                    FrozenPuzzle();
                    if(opening.Elapsed<4.1f)break;
                    Check(PalmVisible(),"Palm anchor is visible during rescue communication");
                    Capture("02-palm-anchor.png");
                    game.Pause();openingTime=opening.Elapsed;
                    fixedPosition=game.player.view.transform.position;fixedRotation=game.player.view.transform.rotation;
                    Next();break;
                case 2:
                    if(Seconds<.5)break;
                    Check(game.Paused&&opening.Running,"Pause keeps opening resumable");
                    Check(Mathf.Approximately(opening.Elapsed,openingTime),"Paused opening clock stays fixed");
                    Check(Vector3.Distance(fixedPosition,game.player.view.transform.position)<.002f&&Quaternion.Angle(fixedRotation,game.player.view.transform.rotation)<.1f,"Paused opening camera stays fixed");
                    FrozenPuzzle();
                    game.ReturnToTitle();Check(ui.Page==ChronoPage.Title,"Paused awakening can return to title");Next();break;
                case 3:
                    if(Seconds<.3)break;
                    Check(Mathf.Approximately(opening.Elapsed,openingTime),"Title preserves unfinished awakening clock");
                    game.Resume();Check(ui.Page==ChronoPage.Awakening&&opening.Running&&!game.Paused,"Continue returns to unfinished awakening");Next();break;
                case 4:
                    if(opening.Running)
                    {
                        FrozenPuzzle();
                        if(opening.Elapsed>=8.4f){Capture("03-standing-up.png");Next();}
                    }
                    else throw new Exception("Opening completed before standing capture");
                    break;
                case 5:
                    if(opening.Running){FrozenPuzzle();break;}
                    Check(opening.Elapsed>=10.5f,"Natural opening completes after its real 10.5 second duration");
                    AssertStanding("Natural finish");Capture("04-control-released.png");
                    Check(game.elapsed<.25f,"Puzzle timer begins only after control release");
                    Next();break;
                case 6:
                    if(Seconds<.3)break;
                    Check(game.elapsed>puzzleTime,"Puzzle timer advances after opening");
                    game.Restart();AssertStanding("Fast restart");Check(!opening.Running,"Restart skips the cinematic");
                    var accessible=ChronoPreferences.Current;accessible.cameraMotion=false;ChronoPreferences.Apply(accessible);
                    game.BeginStory();puzzleTime=game.elapsed;Next();break;
                case 7:
                    FrozenPuzzle();
                    if(opening.Elapsed<3.3f)break;
                    fixedPosition=game.player.view.transform.position;fixedRotation=game.player.view.transform.rotation;
                    Check(PalmVisible(),"Accessible opening still shows palm anchor");
                    Capture("05-accessible-fixed-shot.png");Next();break;
                case 8:
                    FrozenPuzzle();
                    if(opening.Elapsed<8.6f)break;
                    Check(Vector3.Distance(fixedPosition,game.player.view.transform.position)<.002f&&Quaternion.Angle(fixedRotation,game.player.view.transform.rotation)<.1f,"Camera motion disabled keeps a fixed shot through rise phase");
                    opening.Finish();AssertStanding("Skip during standing phase");
                    Check(game.elapsed<.1f,"Skip does not charge the opening duration to puzzle timer");
                    game.BeginStory();puzzleTime=game.elapsed;Next();break;
                case 9:
                    if(opening.Elapsed<.7f)break;
                    opening.Finish();AssertStanding("Skip during eye-opening phase");
                    game.BeginStory();puzzleTime=game.elapsed;Next();break;
                case 10:
                    FrozenPuzzle();
                    if(opening.Elapsed<4)break;
                    opening.Finish();AssertStanding("Skip during palm phase");
                    Capture("06-skipped-standing-room.png");
                    Check(!game.ApplyToTarget(null,TemporalMode.Freeze),"Invalid device input remains rejected after skip");
                    Finish(true,"CHRONO_OPENING_QA_PASSED");return;
            }
        }
        catch(Exception ex){Finish(false,ex.ToString());}
    }
    static double Seconds=>EditorApplication.timeSinceStartup-at;
    static void Next(){stage++;at=EditorApplication.timeSinceStartup;Debug.Log("OPENING_QA_STAGE "+stage);}
    static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);Debug.Log("OPENING_PASS "+label);}
    static void FrozenPuzzle(){if(!Mathf.Approximately(game.elapsed,puzzleTime))throw new Exception("Puzzle timer advanced during opening");}
    static bool PalmVisible()
    {
        return game.Hands && game.Hands.IsOpeningPresentation;
    }
    static void AssertStanding(string label)
    {
        Check(!game.Opening.Running&&game.IsPlaying&&game.Interface.Page==ChronoPage.HUD,label+" restores playing/HUD state");
        Check(Vector3.Distance(game.player.transform.position,game.Opening.StandingPosition)<.1f&&game.player.transform.position.z<0,label+" stays in opening room");
        Check(Vector3.Distance(game.player.view.transform.localPosition,new Vector3(0,1.62f,0))<.002f&&Quaternion.Angle(game.player.view.transform.localRotation,Quaternion.identity)<.1f,label+" restores standing camera pose");
        Check(!PalmVisible(),label+" hides opening palm");
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
            string backup=SessionState.GetString(SettingsKey,"");
            if(!string.IsNullOrEmpty(backup))ChronoPreferences.Apply(JsonUtility.FromJson<ChronoSettingsData>(backup));
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"opening-result.txt"),message+"\nActual real-time Opening component with production menu/pause/finish APIs. Player input component disabled for deterministic testing; no hardware Space/Escape injection and no clock overrides. Camera captures include runtime UI with temporary capture-only HDR/post-processing changes. No saved settings were edited.\n"+string.Join("\n",checks)+"\n"+string.Join("\n",errors));
        }
        catch(Exception ex){Debug.LogError(ex);success=false;}
        if(success)Debug.Log(message);else Debug.LogError("CHRONO_OPENING_QA_FAILED "+message);
        EditorApplication.Exit(success?0:1);
    }
}
