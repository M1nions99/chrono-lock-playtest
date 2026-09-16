using UnityEngine;
using UnityEngine.InputSystem;

namespace ChronoLock
{
    // The opening is part of the same physical room the player explores afterward.
    [DefaultExecutionOrder(50)]
    public sealed class ChronoOpening : MonoBehaviour
    {
        public bool Running { get; private set; }
        public float Elapsed { get; private set; }
        public bool HasRoom => standing && wake;
        public Vector3 StandingPosition => standing ? standing.position : new Vector3(0,1,3);
        public float Darkness => !Running ? 0 : Elapsed < 3 ? Mathf.Lerp(1,0,Mathf.Clamp01((Elapsed-.6f)/2.4f)) : 0;
        public int Phase => Elapsed<3?0:Elapsed<7?1:2;
        public string Caption => Phase==0 ? "[비상 기록] 네메시스 관측 항로… 추진기 응답 없음.\n주인공: 누구든, 내 신호가 들리면…" : Phase==1 ? "이오: 들리나요? 여기는 아르카 구조동입니다.\n손의 문양은 시간 앵커예요. 당신을 살려 두기 위해 연결했습니다." : "이오: 천천히 일어나세요. 당신은 이제 안전합니다.\n귀환 캡슐로 가는 문을 열어 둘게요.";
        ChronoGame game;
        Transform wake, standing, exitDoor;
        Vector3 closedDoor;
        int lastCue=-1;
        public void Initialize(ChronoGame owner)
        {
            game=owner;
            wake=GameObject.Find("Opening wake pose")?.transform;
            standing=GameObject.Find("Opening standing pose")?.transform;
            exitDoor=GameObject.Find("Opening exit door")?.transform;
            if(exitDoor)closedDoor=exitDoor.localPosition;
        }
        public void Begin()
        {
            if(!HasRoom){game.Interface.ShowHUD();return;}
            Running=true;Elapsed=0;lastCue=-1;
            if(exitDoor)exitDoor.localPosition=closedDoor;
            game.Interface.ShowAwakening();game.RefreshCursor();
            ApplyPose();
        }
        public void ResetForRun()
        {
            Running=false;Elapsed=0;lastCue=-1;
            if(exitDoor)exitDoor.localPosition=closedDoor+Vector3.up*4.3f;
        }
        public void Finish()
        {
            if(!Running)return;
            Running=false;
            if(exitDoor)exitDoor.localPosition=closedDoor+Vector3.up*4.3f;
            game.player.Teleport(StandingPosition);game.Resume();
        }
        void Update()
        {
            if(!Running || game.Paused)return;
            if(game.InputReady && Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame){Finish();return;}
            Elapsed+=Time.unscaledDeltaTime;
            if(Phase!=lastCue){lastCue=Phase;game.PlayMenuCue();}
            if(exitDoor)exitDoor.localPosition=Vector3.Lerp(closedDoor,closedDoor+Vector3.up*4.3f,Mathf.Clamp01((Elapsed-7)/3));
            if(Elapsed>=10.5f)Finish();
        }
        void LateUpdate(){if(Running)ApplyPose();}
        void ApplyPose()
        {
            var camera=game.player.view.transform;
            var resting=wake.position;
            var upright=StandingPosition+Vector3.up*1.62f;
            bool motion=ChronoPreferences.Runtime.cameraMotion;
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01((Elapsed-7.2f)/3));
            if(!motion)rise=Elapsed<9.6f?0:1;
            camera.SetPositionAndRotation(Vector3.Lerp(resting,upright,rise),Quaternion.Slerp(wake.rotation,Quaternion.identity,rise));
        }
    }
}

