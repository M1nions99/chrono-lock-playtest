using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ChronoLock;

namespace ChronoStation
{
    public sealed class StationUI : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public StationPage Page { get; private set; }
        StationGame game;
        Font font;
        RectTransform content;
        Text chapter, objective, hint, status, prompt, objectControl, selfControl, timer, reserve;
        Text[] cardText = new Text[4];
        Image[] cards = new Image[4];
        Button[] cardButtons = new Button[4];
        Image progress;
        ChronoSettingsData draft;
        Text saved;
        GameObject targetRoot, reticleRoot;
        bool initialized;
        readonly Color ink = new Color(.035f,.065f,.085f,.97f);
        readonly Color white = new Color(.91f,.96f,1);
        readonly Color muted = new Color(.57f,.68f,.75f);
        readonly Color cyan = new Color(.18f,.89f,.91f);
        readonly Color amber = new Color(1,.69f,.27f);
        readonly string[] names = { "가속", "감속", "정지", "되감기" };
        readonly Color[] accents = { new Color(1,.63f,.22f), new Color(.35f,.65f,1), new Color(.17f,.88f,.89f), new Color(.76f,.51f,1) };

        public void Initialize(StationGame owner)
        {
            if(initialized)return;
            game=owner; initialized=true;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},22);
            var go=new GameObject("Station interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);
            Canvas=go.GetComponent<Canvas>(); Canvas.renderMode=RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder=50;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;
            if(!FindFirstObjectByType<EventSystem>())
            {
                var es=new GameObject("Station input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform,false);
            }
            Show(StationPage.Title);
        }
        void Start(){if(!initialized){var owner=FindFirstObjectByType<StationGame>();if(owner)Initialize(owner);}}
        public void Show(StationPage page)
        {
            if(!initialized)return;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            if(content){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            Page=page; draft=null;
            chapter=objective=hint=status=prompt=objectControl=selfControl=timer=reserve=null;progress=null;
            cards=new Image[4]; cardText=new Text[4]; cardButtons=new Button[4];
            content=Rect(Canvas.transform,"Page "+page,0,0,1280,720);
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);content.anchoredPosition=Vector2.zero;Fit();
            if(page==StationPage.HUD){BuildHUD();RefreshHUD();return;}
            Panel(content,0,0,1280,720,new Color(.015f,.035f,.05f,page==StationPage.Title?.32f:.96f),true);
            if(page==StationPage.Title)Panel(content,40,90,535,545,new Color(.02f,.045f,.06f,.78f));
            Panel(content,48,44,4,32,cyan);
            Label(content,"CHRONO / STATION",68,46,600,28,18,cyan);
            Label(content,"TEMPORAL RESEARCH DIVISION",68,677,700,22,12,muted);
            switch(page)
            {
                case StationPage.Title:BuildTitle();break;
                case StationPage.Pause:BuildPause();break;
                case StationPage.Options:BuildOptions();break;
                case StationPage.Controls:BuildControls();break;
                case StationPage.Completed:BuildComplete();break;
            }
            var first=content.GetComponentInChildren<Button>();
            if(first&&EventSystem.current)EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        void BuildTitle()
        {
            Label(content,"CHRONO",68,113,470,78,61,white);
            Label(content,"STATION",68,183,470,78,61,white);
            Label(content,"먼저 자신의 시간을 배우세요.",76,291,460,37,23,white);
            Label(content,"가속 · 감속 · 정지 · 되감기\n능력을 익히고, 정거장의 시간을 바꾸세요.",76,341,460,65,18,muted);
            float row=431;
            if(game.HasStarted)
            {
                Button(content,"이어하기",76,row,430,50,()=>game.Resume(),true);row+=61;
                Button(content,"새 게임 시작",76,row,430,46,()=>game.NewGame());row+=57;
            }
            else {Button(content,"새 게임 시작",76,row,430,50,()=>game.NewGame(),true);row+=61;}
            Button(content,"설정",76,row,130,43,()=>game.ShowOptions());
            Button(content,"조작법",226,row,130,43,()=>game.ShowControls());
            Button(content,"종료",376,row,130,43,()=>game.Quit());
            Label(content,"연구실  →  연결 통로  →  도킹",76,603,465,24,14,cyan);
        }        void BuildPause()
        {
            Heading("일시정지","게임 진행이 멈춰 있습니다.");
            Button(content,"계속하기",76,255,400,52,()=>game.Resume(),true);
            Button(content,"현재 구역 다시 시도",76,320,400,52,()=>game.Retry());
            Button(content,"설정",76,385,190,52,()=>game.ShowOptions());
            Button(content,"조작법",286,385,190,52,()=>game.ShowControls());
            Button(content,"메인 메뉴",76,450,400,52,()=>game.BackToTitle());
            Label(content,game.StepTitle,550,260,640,44,27,cyan);
            Label(content,game.Objective,550,322,630,140,23,white);
            Label(content,"현재 구역 재시도는 진행 중인 구역의 시작점으로 돌아갑니다.",550,500,630,72,18,muted);
        }
        void BuildHUD()
        {
            Panel(content,24,22,580,123,new Color(.025f,.055f,.075f,.84f));
            chapter=Label(content,"",42,35,540,25,15,cyan);
            objective=Label(content,"",42,69,535,63,22,white);
            timer=Label(content,"",1025,28,230,28,16,muted);timer.alignment=TextAnchor.MiddleRight;
            hint=Label(content,"",42,156,660,67,18,muted);
            var track=Panel(content,42,139,540,3,new Color(.15f,.23f,.27f));
            progress=Panel(track.transform,0,0,540,3,cyan);
            var reticle=Label(content,"┼",626,342,28,32,22,white);reticle.alignment=TextAnchor.MiddleCenter;reticleRoot=reticle.gameObject;
            prompt=Label(content,"",380,390,520,65,20,white);prompt.alignment=TextAnchor.MiddleCenter;
            status=Label(content,"",200,460,880,80,18,amber);status.alignment=TextAnchor.MiddleCenter;
            var controls=Rect(content,"Direct mouse controls",250,546,780,44);targetRoot=controls.gameObject;
            var objectPanel=Panel(controls,0,0,384,44,ink);
            objectControl=Label(objectPanel.transform,"",12,3,360,38,18,cyan);objectControl.alignment=TextAnchor.MiddleCenter;
            var selfPanel=Panel(controls,396,0,384,44,ink);
            selfControl=Label(selfPanel.transform,"우클릭 · 자기 자신",12,3,360,38,18,white);selfControl.alignment=TextAnchor.MiddleCenter;
            var effects=Panel(content,904,60,352,114,new Color(.025f,.055f,.075f,.84f));
            reserve=Label(effects.transform,"",16,9,320,94,17,cyan);reserve.alignment=TextAnchor.UpperRight;
            Label(controls,"휠 / 1~4 능력 선택  ·  Q 능력 해제",50,136,680,26,16,muted).alignment=TextAnchor.MiddleCenter;
            for(int i=0;i<4;i++)
            {
                int index=i;
                var button=Button(content,"",260+i*194,605,182,70,()=>game.SelectAbility((StationAbility)index));
                cards[i]=button.GetComponent<Image>(); cardText[i]=button.GetComponentInChildren<Text>();cardText[i].fontSize=18;
                cardButtons[i]=button;
            }
            Label(content,"ESC  메뉴",27,672,200,24,14,muted);
        }
        void Update(){if(!initialized)return;Fit();if(Page==StationPage.HUD)RefreshHUD();}
        void Fit(){if(!content||!Canvas)return;var size=((RectTransform)Canvas.transform).rect.size;float scale=Mathf.Min(size.x/1280f,size.y/720f);if(scale>0)content.localScale=Vector3.one*scale;}
        void RefreshHUD()
        {
            if(targetRoot)targetRoot.SetActive(!game.Opening);if(reticleRoot)reticleRoot.SetActive(!game.Opening);
            if(reserve)reserve.transform.parent.gameObject.SetActive(!game.Opening);
            Set(chapter,"SECTOR "+(game.Step+1).ToString("00")+" / 08   ·   "+game.StepTitle);
            Set(objective,game.Objective);Set(hint,game.Opening?"":"H  도움말   ·   E  상호작용");
            Set(status,game.Status);Set(prompt,game.UsePrompt);
            Set(timer,ChronoPreferences.Current.showTimer?TimeLabel(game.Elapsed):"");
            Set(objectControl,game.ObjectUnlocked?"좌클릭 · 조준한 사물":"좌클릭 · 사물 (4종 학습 후 해금)");
            objectControl.color=game.ObjectUnlocked?cyan:muted;
            if(reticleRoot)reticleRoot.GetComponent<Text>().color=game.ObjectUnlocked&&game.Focused?cyan:white;
            string selfEffect=game.SelfActive.HasValue?"자기 · "+names[(int)game.SelfActive.Value]+"  "+game.SelfRemaining.ToString("0.0")+"초":game.Cooldown>0?"자기 · 재사용 대기 "+game.Cooldown.ToString("0.0")+"초":"자기 · 효과 없음";
            string objectEffect=game.ActiveObject&&game.ActiveObject.ActiveAbility.HasValue?"사물 · "+names[(int)game.ActiveObject.ActiveAbility.Value]:game.ObjectUnlocked?"사물 · 효과 없음":"사물 · 미해금";
            Set(reserve,game.Opening?"":selfEffect+"\n"+objectEffect);
            if(progress)progress.rectTransform.sizeDelta=new Vector2(540*Mathf.Clamp01(game.StepProgress),3);
            for(int i=0;i<4;i++)
            {
                bool unlocked=i<game.UnlockedAbilities, selected=(int)game.Selected==i;
                cards[i].gameObject.SetActive(!game.Opening);
                Set(cardText[i],(i+1)+"  "+names[i]+(unlocked?"":"  · 잠김"));
                cards[i].color=selected&&unlocked?new Color(accents[i].r*.23f,accents[i].g*.23f,accents[i].b*.23f,.96f):ink;
                cardText[i].color=unlocked?(selected?accents[i]:white):muted;
                cardButtons[i].interactable=unlocked&&!game.Opening;
            }
        }
        void BuildControls()
        {
            Heading("조작법","자기 자신에게 익힌 능력을, 사물에도 적용하세요.");
            string[] keys={"W A S D / 마우스","Space / H","휠 / 1 · 2 · 3 · 4","마우스 오른쪽","마우스 왼쪽","Q / E / Esc"};
            string[] values={"이동 / 시점 돌리기","점프 / 현재 퍼즐 도움말","가속 · 감속 · 정지 · 되감기 선택","자기 자신에게 사용 · 같은 능력 재클릭 시 해제","조준한 사물에 사용 · 같은 능력 재클릭 시 해제","모든 능력 해제 / 상호작용 / 일시정지·뒤로"};
            for(int i=0;i<keys.Length;i++){Label(content,keys[i],80,245+i*47,350,34,20,cyan);Label(content,values[i],430,245+i*47,790,40,20,white);}
            Label(content,"사물 조작은 자기 4종 학습 후 해금됩니다.",80,538,1120,35,18,muted);
            Button(content,"돌아가기",80,591,280,50,()=>game.Back(),true);
        }
        void BuildComplete()
        {
            Heading("귀환 경로 확보","자신의 시간을 넘어, 정거장의 시간을 되찾았습니다.");
            Label(content,"ELAPSED",80,278,400,28,15,cyan);
            Label(content,TimeLabel(game.Elapsed),80,318,480,80,56,white);
            Label(content,"재시도  "+game.Attempts+"회",80,423,700,40,23,muted);
            Button(content,"처음부터 다시",80,535,310,54,()=>game.NewGame(),true);
            Button(content,"메인 메뉴",410,535,280,54,()=>game.BackToTitle());
        }
        void BuildOptions()
        {
            draft=ChronoPreferences.Current;
            Heading("설정","적용을 누르면 저장됩니다. 돌아가면 적용하지 않은 변경은 취소됩니다.");
            SettingSlider("전체 음량",245,0,1,draft.masterVolume,v=>draft.masterVolume=v,v=>Mathf.RoundToInt(v*100)+"%");
            SettingSlider("효과음",308,0,1,draft.effectsVolume,v=>draft.effectsVolume=v,v=>Mathf.RoundToInt(v*100)+"%");
            SettingSlider("마우스 감도",371,.2f,3,draft.sensitivity,v=>draft.sensitivity=v,v=>v.ToString("0.00"));
            SettingSlider("시야각",434,60,100,draft.fov,v=>draft.fov=v,v=>Mathf.RoundToInt(v)+"°");
            Button toggle=null;
            toggle=Button(content,"카메라 흔들림  "+(draft.cameraMotion?"켜짐":"꺼짐"),80,503,430,46,()=>{draft.cameraMotion=!draft.cameraMotion;Set(toggle.GetComponentInChildren<Text>(),"카메라 흔들림  "+(draft.cameraMotion?"켜짐":"꺼짐"));Set(saved,"변경 사항을 적용해 주세요.");});
            Button(content,"적용 및 저장",80,589,270,52,()=>{ChronoPreferences.Save(draft);draft=ChronoPreferences.Current;Set(saved,"설정을 저장했습니다.");},true);
            Button(content,"돌아가기",370,589,230,52,()=>game.Back());
            saved=Label(content,"",650,596,560,40,18,cyan);
        }
        void SettingSlider(string title,float y,float min,float max,float value,Action<float> change,Func<float,string> format)
        {
            Label(content,title,80,y,275,34,22,white);
            var valueText=Label(content,format(value),1090,y,120,34,20,cyan);
            var root=Rect(content,title+" slider",365,y-4,690,42);
            var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var slider=root.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.direction=Slider.Direction.LeftToRight;
            Panel(root,0,18,690,6,new Color(.19f,.28f,.32f));
            var area=Rect(root,"Handle area",12,0,666,42);
            var handle=Panel(area,0,0,24,34,cyan);
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.anchoredPosition=Vector2.zero;
            // Slider drives vertical anchors to stretch; keep the resulting handle inside its row.
            handle.rectTransform.sizeDelta=new Vector2(18,-12);
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v=>{change(v);Set(valueText,format(v));Set(saved,"변경 사항을 적용해 주세요.");});
        }
        void Heading(string title,string subtitle){Label(content,title,76,116,1100,76,48,white);Label(content,subtitle,80,197,1110,36,19,muted);}
        static string TimeLabel(float elapsed){int secs=Mathf.Max(0,Mathf.FloorToInt(elapsed));return (secs/60).ToString("00")+":"+(secs%60).ToString("00");}
        static void Set(Text text,string value){if(text&&text.text!=(value??""))text.text=value??"";}
        RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        Image Panel(Transform parent,float x,float y,float w,float h,Color color,bool raycast=false)
        {var r=Rect(parent,"Panel",x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;}
        Text Label(Transform parent,string value,float x,float y,float w,float h,int size,Color color)
        {
            var r=Rect(parent,"Text",x,y,w,h);var text=r.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.color=color;text.text=value??"";
            text.raycastTarget=false;text.supportRichText=false;text.alignment=TextAnchor.MiddleLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        Button Button(Transform parent,string title,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var image=Panel(parent,x,y,w,h,primary?cyan:new Color(.085f,.14f,.18f),true);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.77f,.95f,1);colors.pressedColor=new Color(.55f,.75f,.82f);colors.selectedColor=new Color(.75f,.94f,1);colors.disabledColor=new Color(.5f,.5f,.5f,.7f);button.colors=colors;
            button.onClick.AddListener(()=>action());
            var label=Label(image.transform,title,16,3,w-32,h-6,21,primary?ink:white);label.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        void OnDestroy(){if(font)Destroy(font);}
    }
}






