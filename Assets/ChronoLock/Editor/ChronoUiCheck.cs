using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChronoLock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

// Production menu API tests. Does not simulate mouse clicks or physical keyboard input.
[InitializeOnLoad]
public static class ChronoUiCheck
{
    const string Key="ChronoLock.UiQA.Running", OutKey="ChronoLock.UiQA.Output";
    const string BackupKey="ChronoLock.UiQA.Backup", HadKey="ChronoLock.UiQA.Had";
    static ChronoGame game;
    static int stage, lastFrame=-1;
    static double started, at;
    static float elapsed, rotor;
    static bool finishing;
    static readonly List<string> checks=new List<string>(), errors=new List<string>();
    static readonly List<string> layoutWarnings=new List<string>();
    static string Output => SessionState.GetString(OutKey,Path.GetFullPath("Builds/UIQA"));
    static ChronoUiCheck() { EditorApplication.update+=Update; Application.logMessageReceived+=OnLog; }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run UI QA from Edit Mode.");
        string output=Path.GetFullPath("Builds/UIQA"); var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++) if(args[i]=="--chrono-ui-out") output=Path.GetFullPath(args[i+1]);
        Directory.CreateDirectory(output); SessionState.SetString(OutKey,output);
        SessionState.SetBool("ChronoLock.UiQA.HadBest",PlayerPrefs.HasKey("ChronoLock.BestTime.v1"));
        SessionState.SetFloat("ChronoLock.UiQA.Best",PlayerPrefs.GetFloat("ChronoLock.BestTime.v1",0));
        SessionState.SetBool(HadKey,PlayerPrefs.HasKey(ChronoPreferences.StorageKey));
        SessionState.SetString(BackupKey,PlayerPrefs.GetString(ChronoPreferences.StorageKey,""));
        EditorSceneManager.OpenScene("Assets/ChronoLock/Scenes/ChronoLab.unity");
        SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
    }
    static void OnLog(string msg,string stack,LogType type)
    {
        if(!SessionState.GetBool(Key,false)||finishing) return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase")) return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(msg+"\n"+stack);
    }
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||finishing) return;
        try
        {
            if(started==0) started=at=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-started>75) throw new Exception("UI QA timeout at "+stage);
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||lastFrame==Time.frameCount) return;
            lastFrame=Time.frameCount;
            if(errors.Count>0) throw new Exception(errors[0]);
            if(!game)
            {
                game=UnityEngine.Object.FindFirstObjectByType<ChronoGame>();
                if(!game||!game.Interface||Time.frameCount<4) { game=null; return; }
                Check(!game.HasStarted&&game.Paused&&!game.IsPlaying,"Boot is paused title before a run");
                Page(ChronoPage.Title); elapsed=game.elapsed;rotor=game.rotor.state;at=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-at<.18) return;
            var ui=game.Interface;
            switch(stage)
            {
                case 0:
                    Frozen(); Check(Cursor.visible,"Title cursor visible"); Capture("01-title.png");
                    Check(!game.ApplyToTarget(game.rotor,TemporalMode.Freeze),"Title blocks temporal action");
                    ui.ShowOptions(); Page(ChronoPage.Options); break;
                case 1:
                    Capture("02-options.png");
                    var original=ChronoPreferences.Current;
                    ui.Draft.masterVolume=original.masterVolume>.5f?.31f:.79f;
                    ui.Back();Page(ChronoPage.Confirm);ui.ConfirmCancel();Page(ChronoPage.Options);
                    Check(!Mathf.Approximately(ui.Draft.masterVolume,original.masterVolume),"Cancel discard preserves draft");
                    ui.Back();ui.ConfirmAccept();Page(ChronoPage.Title);
                    Check(Mathf.Approximately(ChronoPreferences.Current.masterVolume,original.masterVolume),"Discard leaves live settings unchanged");
                    ui.ShowOptions();ui.Draft.masterVolume=.43f;ui.Draft.sensitivity=1.27f;ui.Draft.fov=84;ui.Draft.cameraMotion=false;ui.ApplyOptions();
                    var loaded=ChronoPreferences.Load();
                    Check(Mathf.Approximately(loaded.masterVolume,.43f)&&Mathf.Approximately(loaded.sensitivity,1.27f)&&loaded.fov==84&&!loaded.cameraMotion,"Applied options survive Save/Load");
                    Check(Mathf.Approximately(AudioListener.volume,.43f),"Volume applies to listener");
                    if(ui.Page==ChronoPage.Options) ui.Back();Page(ChronoPage.Title);
                    ui.ShowControls();Page(ChronoPage.Controls);break;
                case 2:
                    Capture("03-controls.png");ui.Back();Page(ChronoPage.Title);ui.StartRun();
                    Check(game.HasStarted&&game.IsPlaying&&!game.Paused,"Start begins run");Page(ChronoPage.HUD);break;
                case 3:
                    Check(game.elapsed>0,"Run timer advances");Capture("04-hud.png");
                    game.Pause();Page(ChronoPage.Pause);elapsed=game.elapsed;rotor=game.rotor.state;break;
                case 4:
                    Frozen();Check(Cursor.visible,"Pause cursor visible");Capture("05-pause.png");
                    Check(!game.ApplyToTarget(game.rotor,TemporalMode.Freeze),"Pause blocks temporal action");
                    ui.ShowOptions();ui.Back();Page(ChronoPage.Pause);ui.ShowControls();ui.Back();Page(ChronoPage.Pause);
                    ui.RequestRestart();Page(ChronoPage.Confirm);ui.ConfirmCancel();Page(ChronoPage.Pause);
                    Check(Mathf.Approximately(game.elapsed,elapsed),"Cancel restart preserves run");
                    game.Resume();Page(ChronoPage.HUD);break;
                case 5:
                    Check(game.elapsed>elapsed,"Resume advances preserved timer");
                    game.ReturnToTitle();Page(ChronoPage.Title);Check(game.HasStarted&&game.Paused,"Return title retains resumable run");
                    elapsed=game.elapsed;rotor=game.rotor.state;break;
                case 6:
                    Frozen();game.Resume();Page(ChronoPage.HUD);
                    game.Pause();ui.RequestRestart();Page(ChronoPage.Confirm);Capture("06-confirm.png");
                    ui.ConfirmAccept();Page(ChronoPage.HUD);
                    Check(game.IsPlaying&&game.elapsed<.1f&&!game.rotorCleared,"Confirmed restart resets run");
                    // Completion setup is synthetic. Puzzle walking is checked separately by ChronoPlayCheck.
                    game.rotorCleared=true;game.rotor.Secure();game.reactor.Secure();game.bridge.Secure();
                    game.player.Teleport(new Vector3(0,1,46));
                    Check(game.TryConfirmTarget(null)&&game.Completed,"Production completion action reaches completed state");
                    elapsed=game.elapsed;rotor=game.rotor.state;break;
                case 7:
                    Page(ChronoPage.Completed);Frozen();Check(Cursor.visible,"Completion cursor visible");Capture("07-completed.png");
                    Check(!game.ApplyToTarget(game.rotor,TemporalMode.Freeze),"Completion blocks temporal action");
                    game.Restart();Check(game.IsPlaying&&!game.Completed&&!game.rotorCleared&&!game.reactor.secured&&!game.bridge.secured,"Replay resets puzzle state");
                    var invalid=ChronoPreferences.Current;invalid.masterVolume=3;invalid.sensitivity=-4;invalid.fov=float.NaN;
                    ChronoPreferences.Apply(invalid);var clean=ChronoPreferences.Current;
                    Check(clean.masterVolume==1&&clean.sensitivity>=.2f&&!float.IsNaN(clean.fov),"Invalid scalar settings sanitized");
                    ChronoPreferences.Load();
                    break;
                case 8:
                    ui.ShowHints();Page(ChronoPage.Hints);
                    Check(game.Paused&&!game.IsPlaying,"Hints opened from HUD pause gameplay");
                    elapsed=game.elapsed;rotor=game.rotor.state;break;
                case 9:
                    Frozen();Capture("08-hints-first.png");
                    ui.AdvanceHint();ui.AdvanceHint();Page(ChronoPage.Hints);break;
                case 10:
                    Frozen();Capture("09-hints-revealed.png");
                    ui.Back();Page(ChronoPage.HUD);Check(game.IsPlaying,"Hints Back returns directly to originating HUD");
                    ui.ShowControls();Page(ChronoPage.Controls);ui.Back();Page(ChronoPage.HUD);
                    Check(game.IsPlaying,"Controls Back returns directly to originating HUD");
                    game.Pause();ui.ShowHints();Page(ChronoPage.Hints);
                    elapsed=game.elapsed;rotor=game.rotor.state;break;
                case 11:
                    Frozen();ui.Back();Page(ChronoPage.Pause);
                    Check(game.Paused&&!game.IsPlaying,"Hints opened from pause return to pause");
                    game.ReturnToTitle();Page(ChronoPage.Title);break;
                case 12:
                    Capture("10-title-1280x720.png",1280,720);
                    Capture("11-title-1024x768.png",1024,768);
                    Capture("12-title-2560x1080.png",2560,1080);
                    ui.ShowOptions();ui.SelectOptionsTab(0);break;
                case 13:
                    Page(ChronoPage.Options);Capture("13-options-display.png");ui.SelectOptionsTab(1);break;
                case 14:
                    Page(ChronoPage.Options);Capture("14-options-controls.png");ui.SelectOptionsTab(2);break;
                case 15:
                    Page(ChronoPage.Options);Capture("15-options-audio.png");ui.SelectOptionsTab(3);break;
                case 16:
                    Page(ChronoPage.Options);Capture("16-options-accessibility.png");
                    ui.Back();Page(ChronoPage.Title);game.Restart();
                    ui.ShowJournal();Page(ChronoPage.Journal);Check(game.Paused,"Journal pauses play");break;
                case 17:
                    Capture("17-journal-rescue.png");
                    Check(ui.RuntimeCanvas.GetComponentsInChildren<Button>().CountLockedRecords()==3,"Only first story record unlocked at start");
                    ui.SelectRecord(3);Capture("18-journal-locked-request.png");
                    ui.Back();Page(ChronoPage.HUD);Check(game.IsPlaying,"Journal Back resumes originating HUD");
                    game.Pause();ui.ShowJournal();ui.Back();Page(ChronoPage.Pause);
                    game.rotorCleared=true;game.reactor.Secure();game.bridge.Secure();ui.ShowJournal();ui.SelectRecord(3);break;
                case 18:
                    Capture("19-journal-return.png");ui.Back();Page(ChronoPage.Pause);
                    game.Resume();game.player.Teleport(new Vector3(0,1,46));game.TryConfirmTarget(null);
                    Page(ChronoPage.Completed);ui.ShowJournal();ui.Back();Page(ChronoPage.Completed);
                    game.ReturnToTitle();ui.ShowIntro();Page(ChronoPage.Story);break;
                case 19:
                    Capture("20-story-intro.png");ui.Back();Page(ChronoPage.Title);
                    ui.ShowIntro();ui.StartRun();Page(ChronoPage.HUD);Check(game.elapsed<.1f,"Intro starts fresh chapter");
                    game.Pause();ui.ShowOptions();ui.SelectOptionsTab(1);break;
                case 20:
                    TestPointerSlider(ui);
                    Capture("21-options-pointer-changed.png");ui.Back();Page(ChronoPage.Confirm);ui.ConfirmAccept();
                    Finish(true,"CHRONO_UI_QA_PASSED");return;
            }
            stage++;at=EditorApplication.timeSinceStartup;
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    static void Page(ChronoPage expected) { Check(game.Interface.Page==expected,"Page "+expected); }
    static void Frozen() { Check(Mathf.Approximately(elapsed,game.elapsed)&&Mathf.Approximately(rotor,game.rotor.state),"Menu/completion freezes timer and rotor"); }
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label);checks.Add(label);Debug.Log("UI_PASS "+label); }
    static int CountLockedRecords(this Button[] buttons)
    {
        int count=0;foreach(var button in buttons)if(button.name.Contains("아직 복원되지 않음")&&!button.interactable)count++;return count;
    }
    static void TestPointerSlider(ChronoInterface ui)
    {
        Canvas.ForceUpdateCanvases();
        var slider=ui.RuntimeCanvas.GetComponentInChildren<Slider>();Check(slider,"Sensitivity slider exists");
        var rect=(RectTransform)slider.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
        var start=Vector3.Lerp(corners[0],corners[2],.25f);var end=Vector3.Lerp(corners[0],corners[2],.75f);
        start.y=end.y=(corners[0].y+corners[1].y)*.5f;
        var pointer=new PointerEventData(EventSystem.current){position=start,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0,"Pointer hits runtime graphics");
        pointer.pointerCurrentRaycast=hits[0];pointer.pointerPressRaycast=hits[0];pointer.pressPosition=start;
        Check(hits[0].gameObject.GetComponentInParent<Slider>()==slider,"Pointer reaches slider through actual GraphicRaycaster");
        pointer.pointerPress=ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
        float clicked=ui.Draft.sensitivity;Check(clicked<1.2f,"Pointer-down changes sensitivity draft");
        pointer.position=end;pointer.delta=end-start;
        ExecuteEvents.Execute(slider.gameObject,pointer,ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slider.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        Check(ui.Draft.sensitivity>clicked+.5f&&ui.OptionsDirty,"Pointer drag changes draft and enables Apply");
        Check(!Mathf.Approximately(ChronoPreferences.Current.sensitivity,ui.Draft.sensitivity),"Pointer changes remain unapplied until Apply");
    }
    static void Capture(string name,int width=1280,int height=720)
    {
        var canvas=game.Interface.RuntimeCanvas;Check(canvas,"Runtime UI canvas exists");
        var camera=game.player.view;var rt=new RenderTexture(width,height,24);
        var oldMode=canvas.renderMode;var oldCam=canvas.worldCamera;var oldDistance=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;Texture2D pixels=null;
        var urp=camera.GetComponent<UniversalAdditionalCameraData>();
        bool oldPost=urp&&urp.renderPostProcessing, oldHDR=camera.allowHDR;
        float oldAspect=camera.aspect, oldNear=camera.nearClipPlane;
        var scaler=canvas.GetComponent<CanvasScaler>();
        var scalerUpdate=typeof(CanvasScaler).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)
            ?? typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;camera.nearClipPlane=.01f;canvas.planeDistance=.05f;
            camera.aspect=width/(float)height;camera.allowHDR=false;
            if(urp)urp.renderPostProcessing=false;
            Canvas.ForceUpdateCanvases();
            // Match the production scaler's own current render-target dimensions before sampling.
            if(scaler&&scalerUpdate!=null)scalerUpdate.Invoke(scaler,null);
            Canvas.ForceUpdateCanvases();
            InspectText(canvas,name);
            Debug.Log("UI_CAPTURE_LAYOUT "+name+" pixels="+width+"x"+height+" canvas="+((RectTransform)canvas.transform).rect+" scale="+canvas.scaleFactor);
            camera.Render();RenderTexture.active=rt;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output,name),pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode=oldMode;canvas.worldCamera=oldCam;canvas.planeDistance=oldDistance;
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            camera.aspect=oldAspect;camera.nearClipPlane=oldNear;camera.allowHDR=oldHDR;if(urp)urp.renderPostProcessing=oldPost;
            if(pixels) UnityEngine.Object.DestroyImmediate(pixels);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            if(scaler&&scalerUpdate!=null)scalerUpdate.Invoke(scaler,null);
            Canvas.ForceUpdateCanvases();
        }
    }
    static void InspectText(Canvas canvas,string capture)
    {
        foreach(var label in canvas.GetComponentsInChildren<Text>())
        {
            if(!label.isActiveAndEnabled||string.IsNullOrWhiteSpace(label.text)||label.text.Length<4)continue;
            float preferred=label.preferredHeight, available=label.rectTransform.rect.height;
            if(preferred>available+2)
            {
                string warning=capture+" | preferred="+preferred.ToString("0.0")+" available="+available.ToString("0.0")+" | "+label.text.Replace("\n"," / ");
                layoutWarnings.Add(warning);Debug.LogWarning("UI_TEXT_LAYOUT_WARNING "+warning);
            }
        }
    }
    static void Finish(bool success,string message)
    {
        if(finishing)return;finishing=true;SessionState.SetBool(Key,false);
        try
        {
            if(SessionState.GetBool(HadKey,false)) PlayerPrefs.SetString(ChronoPreferences.StorageKey,SessionState.GetString(BackupKey,""));
            else PlayerPrefs.DeleteKey(ChronoPreferences.StorageKey);
            if(SessionState.GetBool("ChronoLock.UiQA.HadBest",false)) PlayerPrefs.SetFloat("ChronoLock.BestTime.v1",SessionState.GetFloat("ChronoLock.UiQA.Best",0));
            else PlayerPrefs.DeleteKey("ChronoLock.BestTime.v1");
            PlayerPrefs.Save();ChronoPreferences.Load();
            File.WriteAllText(Path.Combine(Output,"ui-layout-warnings.txt"),"Text preferredHeight > allocated rect height + 2: advisory layout checks, not automatic failures. Manual inspection is required for font metrics, deliberate truncation and two-line body text.\nThese captures do not exercise real pointer hover. Disabled buttons may intentionally be dim; keyboard selection may be present. HDR/post-processing are disabled only while capturing, then restored.\n"+(layoutWarnings.Count==0?"No height warnings detected.":string.Join("\n",layoutWarnings)));
            File.WriteAllText(Path.Combine(Output,"ui-result.txt"),message+"\nProduction UI API/state smoke tests; no physical keyboard or mouse simulation. Completion uses synthetic prerequisite setup. PNGs render runtime Canvas through gameplay camera; real display/input QA remains separate. Cursor lock during Editor focus is not asserted.\n"+string.Join("\n",checks)+"\n"+string.Join("\n",errors));
        }
        catch(Exception e){Debug.LogError(e);success=false;}
        Debug.Log(message);EditorApplication.Exit(success?0:1);
    }
}



