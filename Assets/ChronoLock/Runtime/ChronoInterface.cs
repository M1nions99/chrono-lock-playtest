using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChronoLock
{
    public enum ChronoPage { Title, HUD, Pause, Options, Controls, Completed, Confirm, Hints, Story, Journal, Awakening }

    // Retained UI: gameplay never handles clicks that belong to a menu.
    public sealed class ChronoInterface : MonoBehaviour
    {
        public ChronoPage Page { get; private set; }
        public ChronoSettingsData Draft { get; private set; }
        public Canvas RuntimeCanvas { get; private set; }
        ChronoGame game;
        RectTransform content, modal;
        CanvasGroup pageGroup;
        Font font;
        ChronoPage optionsOrigin, controlsOrigin, modalOrigin;
        ChronoPage hintsOrigin;
        ChronoPage journalOrigin;
        int selectedRecord, transmissionStage = -1;
        bool readingTransmission;
        float transmissionUntil;
        GameObject transmission;
        Text transmissionText;
        Text openingCaption;
        Image openingDarkness;
        int hintStage = -1, hintLevel;
        Action accepted;
        GameObject focusBeforeModal;
        List<Selectable> navigation = new List<Selectable>();
        Text objective, timer, targetName, targetHint, notice, link, progressValue, optionStatus;
        Image progressFill, reticle;
        GameObject targetPanel, toast;
        readonly List<Image> abilityFaces = new List<Image>();
        readonly List<Text> abilityNames = new List<Text>();
        Button applyButton;
        int optionTab;
        float fade = 1;
        string appliedStatus = "";
        readonly Color white = new Color(.91f,.96f,.98f);
        readonly Color muted = new Color(.57f,.68f,.74f);
        readonly Color cyan = new Color(.35f,.91f,.88f);
        readonly Color panel = new Color(.035f,.074f,.10f,.98f);
        readonly Color line = new Color(.19f,.31f,.35f,.6f);
        static readonly string[] ModeLabels = { "정지", "가속", "되감기" };

        public void Initialize(ChronoGame owner)
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 22);
            var canvasObject = new GameObject("Chrono Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            RuntimeCanvas = canvasObject.GetComponent<Canvas>();
            RuntimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RuntimeCanvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (!EventSystem.current)
            {
                var system = new GameObject("Chrono Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system.transform.SetParent(transform, false);
                system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            else if (!EventSystem.current.GetComponent<InputSystemUIInputModule>())
            {
                foreach(var previous in EventSystem.current.GetComponents<BaseInputModule>()) previous.enabled=false;
                EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            ShowTitle();
        }
        public void ShowTitle()
        {
            if (game.IsPlaying) { game.ReturnToTitle(); return; }
            Begin(ChronoPage.Title, true);
            var frame = Frame();
            Label(frame, "ARKA  /  THE LAST RESCUE SIGNAL", 0, 0, 650, 28, 16, cyan);
            Label(frame, "CHRONO", 0, 80, 730, 120, 102, white, FontStyle.Bold);
            Label(frame, "LOCK", 0, 196, 730, 120, 102, white, FontStyle.Bold);
            Label(frame, "시간 너머, 집으로 돌아가는 길.", 6, 331, 650, 36, 26, white);
            Label(frame, "정지 · 가속 · 되감기\n블랙홀 곁의 방주, 아르카에서 눈을 뜨다", 7, 374, 590, 60, 18, muted, FontStyle.Normal, 1.65f);
            float y = 474;
            if (game.HasStarted && !game.Completed)
            {
                MenuButton(frame, "이어하기", "진행 중인 탈출으로 돌아갑니다", 0, y, 480, true, game.Resume); y += 76;
                MenuButton(frame, "새 여정", "", 0, y, 480, false, StartStory); y += 64;
            }
            else { MenuButton(frame, "이야기 시작", "CHAPTER 01  /  마지막 구조 신호", 0, y, 480, true, StartStory); y += 80; }
            SmallButton(frame, "설정", 0, y, 148, ShowOptions);
            SmallButton(frame, "조작 안내", 164, y, 148, ShowControls);
            SmallButton(frame, "종료", 328, y, 152, RequestQuit);
            SmallButton(frame,"배경 이야기",0,y+68,480,ShowIntro);
            Footer("1인칭 시간 조작 퍼즐", "1장 구조동  /  플레이테스트");
            FocusFirst();
        }
        public void StartRun()
        {
            if (game.HasStarted && !game.Completed) { RequestRestart(); return; }
            game.PlayMenuCue(); game.Restart();
        }
        public void StartStory()
        {
            if(game.HasStarted && !game.Completed)
            {
                Confirm("새 여정을 시작할까요?","현재 진행을 초기화하고 구조 캡슐에서 다시 깨어납니다.","새 여정 시작",game.BeginStory);return;
            }
            game.BeginStory();
        }
        public void ShowAwakening()
        {
            Begin(ChronoPage.Awakening,false);
            var veil=Stretch(content,"Opening eyelids");openingDarkness=veil.gameObject.AddComponent<Image>();openingDarkness.color=Color.black;
            var caption=Rect(content,"Opening captions",.5f,0,.5f,0,-580,252,1160,105);
            Box(caption,"Caption background",0,0,1160,105,new Color(.01f,.025f,.038f,.86f));
            openingCaption=Label(caption,"",26,17,1108,78,24,white,FontStyle.Normal,1.5f);
            var f=Frame();Label(f,"ARKA / 구조 프로토콜",0,8,760,38,19,cyan);
            SmallButton(f,"SPACE  건너뛰기",1030,706,290,()=>game.Opening.Finish());
            SmallButton(f,"ESC  메뉴",0,706,220,game.Pause);
            Footer("구조 캡슐 / 생체 시간 동기화","카메라 움직임 효과는 접근성 설정에서 끌 수 있습니다");
            FocusFirst();
        }
        public void ShowIntro()
        {
            Begin(ChronoPage.Story,false);FullShade(1);
            var f=Frame();Header(f,"낯선 천장 아래서","CHAPTER 01  /  마지막 구조 신호");
            Label(f,"마지막으로 기억나는 것은, 검은 별과 끊긴 교신이었다.",0,197,1270,46,28,white);
            Box(f,"Story rule",0,269,1320,1,line);
            Label(f,"01  조난",0,311,220,35,20,cyan);
            Label(f,"탐사선의 항법사 주인공. 블랙홀 네메시스 부근에서 사고를 당한 뒤,\n낯선 외계 정거장의 구조동에서 눈을 뜬다.",242,302,1060,87,24,white,FontStyle.Normal,1.5f);
            Label(f,"02  문양",0,419,220,35,20,cyan);
            Label(f,"손바닥에 새겨진 문양이 푸르게 빛난다. 손을 뻗자 회전하던 장치가 멎는다.\n방 안에는 아무도 없지만, 누군가 통신을 연결한다.",242,410,1060,87,24,white,FontStyle.Normal,1.5f);
            Label(f,"03  응답",0,527,220,35,20,cyan);
            Label(f,"이오: “여기는 아르카. 당신은 구조되었습니다. 집으로 돌아가고 싶다면…\n우선 이 문 너머로 와 주세요. 제가 길을 찾겠습니다.”",242,518,1060,87,24,white,FontStyle.Normal,1.5f);
            SmallButton(f,"메인 메뉴",0,710,270,Back);
            var begin=MenuButton(f,"이야기 시작","구조 캡슐에서 깨어납니다",850,697,470,true,StartStory);
            Footer("ESC  메인 메뉴","목표  /  귀환 캡슐로 가는 길 찾기");
            EventSystem.current?.SetSelectedGameObject(begin.gameObject);
        }
        public void ShowHUD()
        {
            Begin(ChronoPage.HUD, false);
            var f = Frame();
            var top = Box(f, "Objective", 0, 0, 560, 92, new Color(.025f,.055f,.075f,.88f));
            Box(top, "Accent", 0, 0, 3, 92, cyan);
            Label(top, "현재 목표", 22, 13, 490, 21, 15, muted);
            objective = Label(top, "", 21, 40, 510, 36, 23, white);
            timer = Label(f, "", 1060, 2, 260, 32, 23, white);
            timer.alignment = TextAnchor.MiddleRight;
            Label(f, "ESC 메뉴 · F1 조작 · H 힌트 · J 기록", 840, 43, 480, 28, 16, muted).alignment = TextAnchor.MiddleRight;
            var comm=Box(f,"Transmission",0,116,900,130,new Color(.025f,.055f,.075f,.94f));
            transmission=comm.gameObject;
            Label(comm,"수신 중  /  이오     ·     J 기록에서 다시 읽기",20,12,858,24,16,cyan);
            transmissionText=Label(comm,"",20,48,858,77,21,white,FontStyle.Normal,1.45f);
            var aim = Rect(content, "Aim", .5f, .5f, .5f, .5f, -12, 12, 24, 24);
            Box(aim,"Left",0,5,2,14,white);Box(aim,"Right",22,5,2,14,white);
            reticle = Box(aim,"Center",10,11,4,4,white).GetComponent<Image>();
            var target = Rect(content, "Target", .5f,.5f,.5f,.5f,-195,-48,390,103);
            targetPanel = target.gameObject;
            Box(target,"Background",0,0,390,103,new Color(.02f,.045f,.06f,.90f));
            targetName=Label(target,"",18,11,354,30,22,white);
            targetHint=Label(target,"",18,47,354,26,17,cyan);
            Box(target,"Track",18,84,296,3,line);
            progressFill=Box(target,"Progress",18,84,296,3,cyan).GetComponent<Image>();
            progressValue=Label(target,"",323,72,56,25,15,white);
            abilityFaces.Clear(); abilityNames.Clear();
            var bottom=Rect(content,"Abilities",0,0,0,0,60,93,540,60);
            for(int i=0;i<3;i++)
            {
                var card=Box(bottom,"Ability "+(i+1),i*178,0,166,62,panel);
                abilityFaces.Add(card.GetComponent<Image>());
                Label(card,(i+1).ToString(),14,16,30,31,21,Accent(i));
                abilityNames.Add(Label(card,ModeLabels[i],54,15,106,32,22,white));
                Box(card,"Underline",0,60,166,2,Accent(i));
            }
            link=Label(bottom,"",0,-35,650,26,16,cyan);
            var toastRect=Rect(content,"Notice",.5f,0,.5f,0,-350,139,700,45);
            toast=toastRect.gameObject;
            Box(toastRect,"Toast Background",0,0,700,45,new Color(.025f,.055f,.075f,.9f));
            notice=Label(toastRect,"",20,8,660,30,18,white);
            pageGroup.alpha=1; fade=1;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void ShowPause()
        {
            Begin(ChronoPage.Pause,false);
            FullShade(.89f);
            var f=Frame();
            Header(f,"일시정지","PAUSED  /  시간의 흐름을 잠시 멈췄습니다");
            Label(f,"진행 상황",805,204,420,30,18,cyan);
            Label(f,game.Objective,805,251,505,70,29,white,FontStyle.Bold);
            Label(f,"플레이 시간  "+FormatTime(game.elapsed),805,350,430,31,21,muted);
            Label(f,"메뉴를 사용하는 동안 탈출은 멈춥니다.\n메인 메뉴로 나가도 이번 실행의 진행은 유지됩니다.",805,410,470,74,18,muted,FontStyle.Normal,1.6f);
            MenuButton(f,"발견 기록","J  /  통신과 정거장 기록 다시 읽기",805,530,505,false,ShowJournal);
            MenuButton(f,"계속하기","ESC  /  탈출으로 복귀",0,203,520,true,game.Resume);
            MenuButton(f,"체크포인트에서 재개","복구한 장치를 유지하고 안전한 위치로 이동",0,294,520,false,game.RetryCheckpoint);
            MenuButton(f,"설정","",0,385,520,false,ShowOptions);
            MenuButton(f,"조작 안내","",0,453,250,false,ShowControls);
            MenuButton(f,"힌트","",270,453,250,false,ShowHints);
            MenuButton(f,"메인 메뉴","이번 실행의 진행 유지",0,521,520,false,game.ReturnToTitle);
            SmallButton(f,"처음부터 다시",0,642,245,RequestRestart);
            SmallButton(f,"게임 종료",265,642,255,RequestQuit);
            Footer("ESC  계속하기","실행을 종료하면 탈출 진행은 저장되지 않습니다");
            FocusFirst();
        }
        public void ShowCompleted()
        {
            Begin(ChronoPage.Completed,true);
            var f=Frame();
            Label(f,"CHAPTER 01 COMPLETE",0,8,660,30,18,cyan);
            Label(f,"첫 번째 문 너머",0,108,1100,100,68,white,FontStyle.Bold);
            Label(f,"구조동 탈출 · 귀환 캡슐 접근 경로 확보",4,236,1000,40,25,muted);
            Box(f,"Result Rule",0,326,780,1,line);
            Label(f,"이번 기록",0,360,400,25,18,muted);
            Label(f,FormatTime(game.elapsed),0,401,700,100,72,white);
            Label(f,"최고 기록  "+FormatTime(PlayerPrefs.GetFloat("ChronoLock.BestTime.v1",game.elapsed)),0,518,620,32,23,cyan);
            MenuButton(f,"다시 도전","새 여정을 시작합니다",0,614,460,true,game.Restart);
            SmallButton(f,"메인 메뉴",493,614,220,game.ReturnToTitle);
            Box(f,"Ending transmission panel",841,318,479,278,new Color(.018f,.04f,.058f,.97f));
            Label(f,"이오 / 통신 복원",866,338,440,36,23,cyan);
            Label(f,"“지구의 좌표를 찾았어요.\n아직 몇 조각이 부족하지만…\n이제 갈 곳이 생겼네요.”\n\n주인공은 손의 문양을 바라본다.\n자신을 구한 신호는 아직 켜져 있다.",866,390,433,198,20,white,FontStyle.Normal,1.25f);
            SmallButton(f,"발견 기록",1013,614,307,ShowJournal);
            Footer("현재 플레이 구간은 1장 구조동까지입니다","다음 이야기  /  외곽 항법 시설");
            FocusFirst();
        }
        public void ShowOptions()
        {
            if(Page!=ChronoPage.Options)
            {
                if(game.IsPlaying) game.Pause();
                optionsOrigin=Page; Draft=ChronoPreferences.Current.Copy(); optionTab=0; appliedStatus="";
            }
            RenderOptions();
        }
        public bool OptionsDirty => Draft!=null && JsonUtility.ToJson(Draft)!=JsonUtility.ToJson(ChronoPreferences.Current);
        public void SelectOptionsTab(int tab){if(Page!=ChronoPage.Options)return;optionTab=Mathf.Clamp(tab,0,3);RenderOptions();}
        void RenderOptions()
        {
            string previousFocus=Page==ChronoPage.Options && EventSystem.current && EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : null;
            Begin(ChronoPage.Options,false);FullShade(1);
            var f=Frame();Header(f,"설정","OPTIONS  /  나에게 맞는 플레이 환경");
            var tabs=new[]{"화면","조작","오디오","접근성"};
            for(int i=0;i<4;i++){int tab=i;var b=SmallButton(f,tabs[i],i*170,166,156,()=>{optionTab=tab;RenderOptions();});SetButtonBase(b,i==optionTab?new Color(.11f,.26f,.28f):panel);}
            Box(f,"Separator",0,229,1320,1,line);
            float y=266;
            if(optionTab==0)
            {
                ChoiceRow(f,"화면 모드","창 모드 또는 테두리 없는 전체 화면",y,new[]{"창 모드","전체 화면"},()=>Draft.displayMode,v=>Draft.displayMode=v);y+=98;
                ChoiceRow(f,"그래픽 품질","그림자 품질과 표시 거리",y,new[]{"낮음","보통","높음"},()=>Draft.quality,v=>Draft.quality=v);y+=98;
                ChoiceRow(f,"프레임 제한","수직 동기화 사용 시 모니터 주사율이 우선합니다",y,new[]{"60 FPS","120 FPS","제한 없음"},()=>Draft.frameLimit,v=>Draft.frameLimit=v);y+=98;
                ToggleRow(f,"수직 동기화","화면이 갈라지는 현상을 줄입니다",y,()=>Draft.vsync,v=>Draft.vsync=v);
            }
            else if(optionTab==1)
            {
                SliderRow(f,"마우스 감도","낮을수록 시점을 천천히 돌립니다",y,.25f,2.5f,()=>Draft.sensitivity,v=>Draft.sensitivity=v,v=>v.ToString("0.00")+"×");y+=114;
                SliderRow(f,"시야각","화면에 보이는 주변 공간의 범위",y,60,100,()=>Draft.fov,v=>Draft.fov=v,v=>Mathf.RoundToInt(v)+"°");y+=114;
                ToggleRow(f,"상하 시점 반전","마우스의 위아래 방향을 바꿉니다",y,()=>Draft.invertY,v=>Draft.invertY=v);
            }
            else if(optionTab==2)
            {
                SliderRow(f,"전체 음량","게임의 모든 소리를 조절합니다",y,0,1,()=>Draft.masterVolume,v=>Draft.masterVolume=v,v=>Mathf.RoundToInt(v*100)+"%");y+=122;
                SliderRow(f,"효과음","시간 장치와 메뉴 선택 소리",y,0,1,()=>Draft.effectsVolume,v=>Draft.effectsVolume=v,v=>Mathf.RoundToInt(v*100)+"%");
                Label(f,"설정을 적용한 뒤 소리가 변경됩니다.",0,560,800,30,18,muted);
            }
            else
            {
                ToggleRow(f,"카메라 움직임 효과","끄면 걷기 흔들림·착지 반동·달리기 시야 변화를 줄입니다",y,()=>Draft.cameraMotion,v=>Draft.cameraMotion=v);y+=112;
                ToggleRow(f,"플레이 시간 표시","플레이 화면 오른쪽 위의 시간을 표시합니다",y,()=>Draft.showTimer,v=>Draft.showTimer=v);y+=120;
                Label(f,"능력은 색상과 함께 이름·숫자로 구분합니다.\n메뉴는 마우스 또는 방향키·Enter로 조작할 수 있습니다.",0,y,1090,75,19,muted,FontStyle.Normal,1.7f);
            }
            optionStatus=Label(f,appliedStatus,0,665,715,31,18,cyan);
            SmallButton(f,"기본값 복원",0,710,220,RestoreDefaults);
            SmallButton(f,"뒤로",834,710,210,Back);
            applyButton=SmallButton(f,"변경 적용",1064,710,256,ApplyOptions);
            RefreshDirty();
            Footer("ESC  뒤로","변경 적용을 눌러야 설정이 저장됩니다");
            if(!FocusNamed(previousFocus))FocusFirst();
        }
        public void ApplyOptions()
        {
            if(Draft==null)return;
            ChronoPreferences.Save(Draft);Draft=ChronoPreferences.Current.Copy();
            appliedStatus="설정을 저장했습니다.";game.PlayMenuCue();RenderOptions();
        }
        public void RestoreDefaults(){Draft=ChronoPreferences.Defaults();appliedStatus="기본값을 선택했습니다. 적용하면 저장됩니다.";RenderOptions();}
        void RefreshDirty()
        {
            bool dirty=OptionsDirty;
            if(applyButton){applyButton.interactable=dirty;foreach(var label in applyButton.GetComponentsInChildren<Text>())label.color=dirty?white:new Color(.36f,.46f,.50f);}
            if(optionStatus)optionStatus.text=dirty?"저장하지 않은 변경사항이 있습니다.":appliedStatus;
        }
        public void ShowControls()
        {
            bool fromPlay=game.IsPlaying;
            if(game.IsPlaying)game.Pause();
            controlsOrigin=fromPlay?ChronoPage.HUD:Page;
            Begin(ChronoPage.Controls,false);FullShade(1);
            var f=Frame();Header(f,"조작 안내","CONTROLS  /  시간을 다루는 방법");
            ControlRow(f,0,210,"W A S D","이동","마우스로 주변을 살펴봅니다.");
            ControlRow(f,0,334,"SHIFT / SPACE","달리기 / 점프","위험 구역을 빠르게 통과하세요.");
            ControlRow(f,0,458,"1 / 2 / 3","정지 / 가속 / 되감기","원하는 시간 능력을 선택합니다.");
            ControlRow(f,713,210,"마우스 왼쪽","능력 적용","9m 이내의 시간 장치를 조준합니다.");
            ControlRow(f,713,334,"마우스 오른쪽","능력 해제","현재 장치를 정상 시간으로 돌립니다.");
            ControlRow(f,713,458,"E","복구 확정 / 탈출","충전·복구 완료 뒤 확정합니다.");
            Box(f,"Rule",0,599,1320,1,line);
            Label(f,"ESC 메뉴  ·  F1 조작  ·  H 힌트  ·  J 발견 기록  ·  R 재시작 확인",0,640,1320,37,21,muted);
            SmallButton(f,"돌아가기",0,710,300,Back);Footer("ESC  돌아가기","조작 안내를 보는 동안 탈출은 멈춥니다");FocusFirst();
        }
        public void ResetHints(){hintStage=-1;hintLevel=0;transmissionStage=-1;transmissionUntil=0;selectedRecord=0;readingTransmission=false;}
        public void ShowJournal()
        {
            if(!game.HasStarted)return;
            if(Page!=ChronoPage.Journal)
            {
                bool fromPlay=game.IsPlaying;
                if(fromPlay)game.Pause();
                journalOrigin=fromPlay?ChronoPage.HUD:Page;
                selectedRecord=ChronoNarrative.Stage(game);
                readingTransmission=false;
            }
            RenderJournal();
        }
        public void SelectRecord(int index)
        {
            if(Page!=ChronoPage.Journal || index<0 || index>ChronoNarrative.Stage(game))return;
            selectedRecord=index;RenderJournal();
        }
        void RenderJournal()
        {
            Begin(ChronoPage.Journal,false);FullShade(1);
            var f=Frame();Header(f,"발견 기록","ARCHIVE  /  문양에 남은 기억");
            int stage=ChronoNarrative.Stage(game);
            for(int i=0;i<4;i++)
            {
                int record=i;bool unlocked=i<=stage;
                var button=MenuButton(f,unlocked?ChronoNarrative.RecordTitles[i]:"0"+(i+1)+"  아직 복원되지 않음","",0,211+i*84,399,false,()=>SelectRecord(record));
                button.interactable=unlocked;
                if(i==selectedRecord)SetButtonBase(button,new Color(.09f,.23f,.24f));
            }
            Label(f,(stage+1)+" / 4 기록 복원",0,583,395,35,20,cyan);
            Box(f,"Archive separator",437,205,1,455,line);
            var recordTab=SmallButton(f,"발견 기록",475,200,219,()=>{readingTransmission=false;RenderJournal();});
            var commTab=SmallButton(f,"구간 통신",710,200,219,()=>{readingTransmission=true;RenderJournal();});
            SetButtonBase(readingTransmission?commTab:recordTab,new Color(.09f,.23f,.24f));
            Label(f,readingTransmission?"이오 / 구조동 통신":ChronoNarrative.RecordSources[selectedRecord],475,277,840,31,18,cyan);
            Label(f,readingTransmission?ChronoNarrative.Briefings[selectedRecord]:ChronoNarrative.Records[selectedRecord],475,326,836,370,21,white,FontStyle.Normal,1.25f);
            SmallButton(f,journalOrigin==ChronoPage.HUD?"플레이로 돌아가기":"돌아가기",975,710,345,Back);
            Footer("ESC  돌아가기","기록은 구역을 복구하면 자동으로 해제됩니다");
            if(!FocusNamed(ChronoNarrative.RecordTitles[selectedRecord]))FocusFirst();
        }
        public void ShowHints()
        {
            if(!game.HasStarted || game.Completed)return;
            if(Page!=ChronoPage.Hints)
            {
                bool fromPlay=game.IsPlaying;
                if(fromPlay)game.Pause();
                hintsOrigin=fromPlay?ChronoPage.HUD:Page;
            }
            int stage=!game.rotorCleared?0:!game.reactor.secured?1:!game.bridge.secured?2:3;
            if(stage!=hintStage){hintStage=stage;hintLevel=0;}
            RenderHints();
        }
        public void AdvanceHint(){if(Page!=ChronoPage.Hints)return;hintLevel=Mathf.Min(2,hintLevel+1);RenderHints();}
        void RenderHints()
        {
            string[][] hints={
                new[]{"앞의 환기 장치는 회전하는 동안 위험합니다.\n장치와 연결된 콘솔을 찾아보세요.","장치의 시간을 멈추면 움직임도 멈춥니다.\n능력을 적용한 뒤에는 같은 장치를 다시 조준할 필요가 없습니다.","로터나 왼쪽 콘솔을 9m 이내에서 조준하세요.\n[1] → [좌클릭]으로 정지한 뒤 회전 구역을 통과하세요."},
                new[]{"다음 문을 열려면 발전기에 전력을 모아야 합니다.\n현재 충전 상태는 발전기를 조준하면 보입니다.","가속 능력으로 발전기의 충전 시간을 앞당길 수 있습니다.\n충전 뒤에는 전력 공급을 확정해야 합니다.","발전기 몸체나 콘솔을 조준하세요.\n[2] → [좌클릭], 충전 100%에서 같은 장치를 조준하고 [E]를 누르세요."},
                new[]{"끊긴 통로는 사고 전에는 연결되어 있었습니다.\n근처 콘솔에는 그때의 상태가 기록되어 있습니다.","되감기 능력으로 사고 전의 통로를 되찾을 수 있습니다.\n복구한 통로는 고정한 뒤 건너세요.","보라색 콘솔을 조준하고 [3] → [좌클릭].\n복구 100%에서 [E]로 고정한 뒤 다리를 건너세요."},
                new[]{"모든 시간 장치가 복구되었습니다.\n마지막 에어록으로 이동하세요.","끝의 문에 충분히 가까이 다가가면 탈출 안내가 표시됩니다.","마지막 에어록 앞에서 [E]를 눌러 탈출을 완료하세요."}
            };
            if(game.Opening && game.Opening.HasRoom && game.player.transform.position.z<3 && !game.rotorCleared)
                hints[0]=new[]{"당신이 깨어난 곳은 아르카의 구조실입니다.\n마우스로 주위를 둘러보면 구조 침상과 관측창을 볼 수 있습니다.","바닥의 푸른 유도등이 열린 출구로 이어집니다.\nWASD로 이동해 문 너머의 통로로 나가세요.","RESCUE / 01 표시 아래 열린 문을 지나 앞으로 이동하세요.\n회전하는 장치가 보이면 콘솔에 다가가 [1] → [좌클릭]으로 정지합니다."};
            Begin(ChronoPage.Hints,false);FullShade(1);
            var f=Frame();Header(f,"막혔을 때","HINTS  /  필요한 만큼만 확인하세요");
            Label(f,game.Objective,0,202,1280,50,27,white,FontStyle.Bold);
            for(int i=0;i<3;i++)
            {
                bool revealed=i<=hintLevel;float y=288+i*116;
                Box(f,"Hint rail "+i,0,y,3,86,revealed?cyan:line);
                Label(f,"0"+(i+1),25,y+2,90,35,25,revealed?cyan:muted);
                Label(f,revealed?hints[hintStage][i]:"다음 힌트를 열면 표시됩니다.",118,y,1190,98,23,revealed?white:muted,FontStyle.Normal,1.5f);
            }
            var next=SmallButton(f,hintLevel<2?"다음 힌트 보기":"모든 힌트 확인",0,710,340,AdvanceHint);next.interactable=hintLevel<2;
            SmallButton(f,hintsOrigin==ChronoPage.HUD?"탈출으로 돌아가기":"일시정지 메뉴",980,710,340,Back);
            Footer("ESC  돌아가기","힌트를 보는 동안 탈출은 멈춥니다");FocusFirst();
        }

        public void Back()
        {
            switch(Page)
            {
                case ChronoPage.HUD: game.Pause();break;
                case ChronoPage.Awakening:game.Pause();break;
                case ChronoPage.Pause:game.Resume();break;
                case ChronoPage.Options:
                    if(OptionsDirty) Confirm("변경사항을 버릴까요?","적용하지 않은 설정은 저장되지 않습니다.","변경 버리기",()=>Return(optionsOrigin));
                    else Return(optionsOrigin);
                    break;
                case ChronoPage.Controls:Return(controlsOrigin);break;
                case ChronoPage.Hints:Return(hintsOrigin);break;
                case ChronoPage.Story:ShowTitle();break;
                case ChronoPage.Journal:Return(journalOrigin);break;
                case ChronoPage.Confirm:ConfirmCancel();break;
                case ChronoPage.Completed:game.ReturnToTitle();break;
            }
        }
        void Return(ChronoPage page)
        {
            if(page==ChronoPage.HUD)game.Resume();
            else if(page==ChronoPage.Title)ShowTitle();
            else if(page==ChronoPage.Completed)ShowCompleted();
            else ShowPause();
        }
        public void RequestRestart()
        {
            if(Page==ChronoPage.Options || Page==ChronoPage.Controls || Page==ChronoPage.Confirm || Page==ChronoPage.Hints || Page==ChronoPage.Journal || Page==ChronoPage.Story || Page==ChronoPage.Awakening)return;
            if(!game.HasStarted){StartRun();return;}
            if(game.IsPlaying)game.Pause();
            Confirm("탈출을 처음부터 시작할까요?","현재 탈출의 진행과 시간이 초기화됩니다.\n저장한 옵션과 최고 기록은 유지됩니다.","처음부터 시작",game.Restart);
        }
        public void RequestQuit()
        {
            if(game.IsPlaying)game.Pause();
            Confirm("게임을 종료할까요?","옵션과 최고 기록은 저장됩니다.\n진행 중인 탈출은 저장되지 않습니다.","게임 종료",()=>{
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            });
        }
        void Confirm(string title,string description,string action,Action callback)
        {
            if(Page==ChronoPage.Confirm)return;
            modalOrigin=Page;Page=ChronoPage.Confirm;accepted=callback;
            focusBeforeModal=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            pageGroup.interactable=false;pageGroup.blocksRaycasts=false;
            modal=Stretch(RuntimeCanvas.transform,"Confirmation");
            var bg=modal.gameObject.AddComponent<Image>();bg.color=new Color(.005f,.014f,.023f,.82f);
            var f=Rect(modal,"Dialog",.5f,.5f,.5f,.5f,-405,190,810,380);
            Box(f,"Panel",0,0,810,380,panel);Box(f,"Top line",0,0,810,2,cyan);
            Label(f,"CONFIRM",38,29,730,24,15,cyan);
            Label(f,title,38,80,730,58,34,white,FontStyle.Bold);
            Label(f,description,40,156,730,86,21,muted,FontStyle.Normal,1.6f);
            var cancel=SmallButton(f,"취소",38,293,342,ConfirmCancel);
            SmallButton(f,action,398,293,374,ConfirmAccept);
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }
        public void ConfirmCancel(){if(Page!=ChronoPage.Confirm)return;CloseModal();if(focusBeforeModal && focusBeforeModal.activeInHierarchy)EventSystem.current?.SetSelectedGameObject(focusBeforeModal);}
        public void ConfirmAccept(){if(Page!=ChronoPage.Confirm)return;var callback=accepted;CloseModal();callback?.Invoke();}
        void CloseModal(){if(modal){modal.gameObject.SetActive(false);Destroy(modal.gameObject);}Page=modalOrigin;pageGroup.interactable=true;pageGroup.blocksRaycasts=true;accepted=null;}
        void Update()
        {
            if(!game || !content)return;
            fade=Mathf.MoveTowards(fade,1,Time.unscaledDeltaTime*7);
            if(pageGroup)pageGroup.alpha=fade;
            if(Page==ChronoPage.Awakening)
            {
                openingCaption.text=game.Opening.Caption;
                openingDarkness.color=new Color(0,0,0,game.Opening.Darkness);
            }
            else if(Page==ChronoPage.HUD)
            {
                objective.text=game.Objective;
                timer.text=ChronoPreferences.Runtime.showTimer?FormatTime(game.elapsed):"";
                int storyStage=ChronoNarrative.Stage(game);
                bool exploring=game.Opening && game.Opening.HasRoom && game.player.transform.position.z<3 && !game.rotorCleared;
                int transmissionKey=exploring?4:storyStage;
                if(transmissionKey!=transmissionStage){transmissionStage=transmissionKey;transmissionUntil=game.elapsed+14;}
                transmission.SetActive(game.elapsed<transmissionUntil);
                transmissionText.text=exploring?"이오: 이곳은 조난자를 위한 구조실입니다. 당신을 해치려는 곳이 아니에요.\n주변을 살펴보세요. 준비되면 바닥의 푸른 불빛을 따라 문으로 오세요.":ChronoNarrative.Briefings[storyStage];
                var t=game.CurrentTarget;
                bool escape=game.bridge.secured && game.player.transform.position.z>45;
                targetPanel.SetActive(t || escape);
                if(t)
                {
                    targetName.text=t.displayName;
                    targetHint.text=t.secured?"안전 잠금 완료":t.Ready&&t.kind!=DeviceKind.Rotor?"[E]  "+(t.kind==DeviceKind.Reactor?"전력 공급 확정":"다리 복구 확정"):"[좌클릭]  "+ChronoGame.ModeName(game.SelectedMode)+" 적용";
                    float progress=t.kind==DeviceKind.Reactor?t.state:t.kind==DeviceKind.Bridge?1-t.state:0;
                    progressFill.rectTransform.sizeDelta=new Vector2(Mathf.Clamp01(progress)*296,3);
                    progressValue.text=t.kind==DeviceKind.Rotor?"":Mathf.RoundToInt(progress*100)+"%";
                }
                else if(escape){targetName.text="탈출 에어록";targetHint.text="[E]  연구소 탈출";progressFill.rectTransform.sizeDelta=new Vector2(296,3);progressValue.text="";}
                reticle.color=t?cyan:white;
                for(int i=0;i<3;i++){bool chosen=(int)game.SelectedMode==i+1;abilityFaces[i].color=chosen?new Color(.09f,.20f,.23f,.96f):panel;abilityNames[i].color=chosen?white:muted;}
                link.text=game.ActiveDevice?"연결됨  ·  "+game.ActiveDevice.displayName+"   /   우클릭 해제":"좌클릭으로 조준한 장치에 적용";
                toast.SetActive(!string.IsNullOrEmpty(game.Notice));notice.text=game.Notice;
            }
            else if(Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                var available=navigation.FindAll(s=>s && s.gameObject.activeInHierarchy && s.IsInteractable() && (Page!=ChronoPage.Confirm || s.transform.IsChildOf(modal)));
                if(available.Count>0)
                {
                    int current=available.FindIndex(s=>EventSystem.current && s.gameObject==EventSystem.current.currentSelectedGameObject);
                    int direction=Keyboard.current.shiftKey.isPressed?-1:1;
                    int next=(current+direction+available.Count)%available.Count;
                    EventSystem.current?.SetSelectedGameObject(available[next].gameObject);
                }
            }
        }
        void Begin(ChronoPage next,bool illustrated)
        {
            if(modal){Destroy(modal.gameObject);modal=null;}
            if(content){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            Page=next; navigation.Clear();fade=1;
            content=Stretch(RuntimeCanvas.transform,next.ToString());
            pageGroup=content.gameObject.AddComponent<CanvasGroup>();
            if(illustrated)
            {
                var artwork=Resources.Load<Texture2D>("TitleBackground");
                if(artwork)
                {
                    var backdrop=Stretch(content,"Blender title artwork");
                    var picture=backdrop.gameObject.AddComponent<RawImage>();picture.texture=artwork;picture.raycastTarget=false;
                    var fit=backdrop.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=artwork.width/(float)artwork.height;
                    var tint=Stretch(content,"Title atmosphere");tint.gameObject.AddComponent<Image>().color=new Color(.012f,.03f,.045f,.20f);tint.GetComponent<Image>().raycastTarget=false;
                }
                else {content.gameObject.AddComponent<CanvasRenderer>();var art=content.gameObject.AddComponent<ChronoBackdrop>();art.raycastTarget=false;}
            }
        }
        void FullShade(float opacity){var shade=content.gameObject.AddComponent<Image>();shade.color=new Color(.018f,.04f,.058f,opacity);}
        RectTransform Frame(){return Rect(content,"Safe Frame",.5f,.5f,.5f,.5f,-660,390,1320,780);}
        void Header(RectTransform f,string text,string caption){Label(f,caption,0,0,1320,29,16,cyan);Label(f,text,0,58,1320,86,58,white,FontStyle.Bold);}
        void Footer(string left,string right)
        {
            var f=Rect(content,"Footer",.5f,0,.5f,0,-660,40,1320,30);
            Label(f,left,0,0,700,26,15,muted);Label(f,right,660,0,660,26,15,muted).alignment=TextAnchor.MiddleRight;
        }
        void ControlRow(RectTransform f,float x,float y,string key,string title,string description)
        {
            Label(f,key,x,y,580,26,17,cyan);
            Label(f,title,x,y+38,580,38,28,white,FontStyle.Bold);
            Label(f,description,x,y+83,590,29,18,muted);
        }
        void ChoiceRow(RectTransform f,string name,string help,float y,string[] values,Func<int> read,Action<int> write)
        {
            Label(f,name,0,y,625,33,23,white);Label(f,help,0,y+39,660,27,16,muted);
            var previous=SmallButton(f,"‹",780,y+5,64,()=>{write((read()+values.Length-1)%values.Length);RenderOptions();});previous.name=name+" Previous";
            Label(f,values[Mathf.Clamp(read(),0,values.Length-1)],860,y+14,351,35,23,white).alignment=TextAnchor.MiddleCenter;
            var next=SmallButton(f,"›",1256,y+5,64,()=>{write((read()+1)%values.Length);RenderOptions();});next.name=name+" Next";
        }
        void ToggleRow(RectTransform f,string name,string help,float y,Func<bool> read,Action<bool> write)
        {
            Label(f,name,0,y,670,32,23,white);Label(f,help,0,y+39,930,30,16,muted);
            var b=SmallButton(f,read()?"켜짐  ✓":"꺼짐",1080,y+1,240,()=>{write(!read());RenderOptions();});
            b.name=name;SetButtonBase(b,read()?new Color(.09f,.23f,.24f):panel);
        }
        void SliderRow(RectTransform f,string name,string help,float y,float min,float max,Func<float> read,Action<float> write,Func<float,string> format)
        {
            Label(f,name,0,y,625,33,23,white);Label(f,help,0,y+41,670,27,16,muted);
            var value=Label(f,format(read()),1190,y,130,35,24,cyan);value.alignment=TextAnchor.MiddleRight;
            var r=Rect(f,name+" Slider",0,1,0,1,780,-y,380,53);
            r.gameObject.AddComponent<Image>().color=Color.clear;
            var slider=r.gameObject.AddComponent<Slider>();
            var track=Box(r,"Track",0,23,380,4,line);
            var fillArea=Rect(r,"Fill Area",0,0,1,1,0,0,0,0);fillArea.offsetMin=new Vector2(0,23);fillArea.offsetMax=new Vector2(0,-26);
            var fill=Stretch(fillArea,"Fill");fill.gameObject.AddComponent<Image>().color=cyan;
            var handleArea=Stretch(r,"Handle Area");handleArea.offsetMin=new Vector2(8,17);handleArea.offsetMax=new Vector2(-8,-16);
            var handle=Rect(handleArea,"Handle",0,.5f,0,.5f,0,0,16,0);
            handle.gameObject.AddComponent<Image>().color=white;
            handle.pivot=new Vector2(.5f,.5f);handle.sizeDelta=new Vector2(16,0);
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.direction=Slider.Direction.LeftToRight;
            slider.minValue=min;slider.maxValue=max;slider.value=read();
            slider.onValueChanged.AddListener(v=>{write(v);value.text=format(v);appliedStatus="";RefreshDirty();});
            navigation.Add(slider);
        }
        Button MenuButton(RectTransform f,string text,string sub,float x,float y,float width,bool primary,Action click)
        {
            var b=Button(f,text,x,y,width,string.IsNullOrEmpty(sub)?58:72,primary,click);
            Label((RectTransform)b.transform,text,22,11,width-60,36,27,primary?new Color(.025f,.08f,.10f):white,FontStyle.Bold);
            if(!string.IsNullOrEmpty(sub))Label((RectTransform)b.transform,sub,23,47,width-70,22,14,primary?new Color(.11f,.29f,.31f):muted);
            Label((RectTransform)b.transform,"›",width-44,13,30,40,30,primary?panel:cyan);
            return b;
        }
        Button SmallButton(RectTransform f,string text,float x,float y,float width,Action click)
        {
            var b=Button(f,text,x,y,width,52,false,click);
            Label((RectTransform)b.transform,text,8,9,width-16,34,20,white).alignment=TextAnchor.MiddleCenter;
            return b;
        }
        Button Button(RectTransform f,string text,float x,float y,float width,float height,bool primary,Action click)
        {
            var r=Box(f,text,x,y,width,height,primary?cyan:panel);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            r.GetComponent<Image>().color=Color.white;var c=b.colors;c.normalColor=primary?cyan:panel;c.highlightedColor=primary?new Color(.62f,1,.96f):new Color(.10f,.25f,.29f);c.selectedColor=c.highlightedColor;c.pressedColor=primary?new Color(.31f,.7f,.69f):new Color(.08f,.18f,.22f);c.disabledColor=new Color(.027f,.046f,.061f);c.fadeDuration=.12f;b.colors=c;
            b.enabled=false;b.enabled=true;
            Box(r,"Lower edge",0,height-1,width,1,primary?cyan:line).GetComponent<Image>().raycastTarget=false;
            b.onClick.AddListener(()=>{game.PlayMenuCue();click();});
            navigation.Add(b);return b;
        }
        RectTransform Box(Transform p,string name,float x,float y,float w,float h,Color c){var r=Rect(p,name,0,1,0,1,x,-y,w,h);r.gameObject.AddComponent<Image>().color=c;return r;}
        Text Label(Transform p,string text,float x,float y,float w,float h,int size,Color color,FontStyle style=FontStyle.Normal,float spacing=1)
        {
            var r=Rect(p,"Text "+text,0,1,0,1,x,-y,w,h);
            var t=r.gameObject.AddComponent<Text>();t.text=text;t.font=font;t.fontSize=size;t.fontStyle=style;t.color=color;t.raycastTarget=false;
            t.alignment=TextAnchor.UpperLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.lineSpacing=spacing;return t;
        }
        static RectTransform Rect(Transform p,string name,float ax,float ay,float bx,float by,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);
            r.anchorMin=new Vector2(ax,ay);r.anchorMax=new Vector2(bx,by);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static RectTransform Stretch(Transform p,string name){var r=Rect(p,name,0,0,1,1,0,0,0,0);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
        static void SetButtonBase(Button button,Color color){var colors=button.colors;colors.normalColor=color;button.colors=colors;}
        bool FocusNamed(string name){if(string.IsNullOrEmpty(name))return false;var item=navigation.Find(s=>s && s.name==name && s.IsInteractable());if(!item)return false;EventSystem.current?.SetSelectedGameObject(item.gameObject);return true;}
        void FocusFirst(){foreach(var item in navigation)if(item && item.IsInteractable()){EventSystem.current?.SetSelectedGameObject(item.gameObject);break;}}
        static Color Accent(int i)=>i==0?new Color(.35f,.91f,.88f):i==1?new Color(1,.66f,.34f):new Color(.73f,.61f,1);
        public static string FormatTime(float seconds){int m=Mathf.FloorToInt(seconds/60);return m.ToString("00")+":"+(seconds-m*60).ToString("00.00");}
        void OnDestroy(){if(font)Destroy(font);}
    }
}



