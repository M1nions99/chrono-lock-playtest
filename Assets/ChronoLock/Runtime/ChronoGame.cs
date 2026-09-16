using UnityEngine;
using UnityEngine.InputSystem;

namespace ChronoLock
{
    public sealed class ChronoGame : MonoBehaviour
    {
        public ChronoPlayer player;
        public TemporalDevice rotor, reactor, bridge;
        public Transform powerDoor, exitDoor;
        public bool Paused { get; private set; }
        public bool Completed { get; private set; }
        public bool rotorCleared;
        public float elapsed;
        TemporalDevice target, active;
        TemporalMode selected = TemporalMode.Freeze;
        Vector3 checkpoint = new Vector3(0, 1, 3);
        string notice = "아르카 구조동 · 귀환 캡슐로 가는 길을 찾으세요.";
        float noticeUntil = 6;
        ChronoFeedback feedback;
        public bool HasStarted { get; private set; }
        public bool IsPlaying => HasStarted && !Paused && !Completed && !(Opening && Opening.Running);
        public ChronoInterface Interface { get; private set; }
        public ChronoOpening Opening { get; private set; }
        public ChronoWrist Hands { get; private set; }
        public string Notice => elapsed < noticeUntil ? notice : string.Empty;
        int inputReadyFrame;
        public bool InputReady => Time.frameCount > inputReadyFrame;
        public TemporalMode SelectedMode => selected;
        public TemporalDevice ActiveDevice => active;
        public TemporalDevice CurrentTarget => target;
        public string Objective => !rotorCleared && Opening && Opening.HasRoom && player.transform.position.z<3 ? "구조동을 살펴보고 출구 찾기" : !rotorCleared ? "01  환기 구역 통과" : !reactor.secured ? "02  구조동 전력 복구" : !bridge.secured ? "03  끊어진 통로 복구" : "04  귀환 캡슐 경로 확보";
        void Awake() { Paused = true; }
        void Start()
        {
            ChronoPreferences.Load();
            feedback = GetComponent<ChronoFeedback>();
            if (!feedback) feedback = gameObject.AddComponent<ChronoFeedback>();
            Opening=GetComponent<ChronoOpening>();
            if(!Opening)Opening=gameObject.AddComponent<ChronoOpening>();
            Opening.Initialize(this);
            Hands=player.view.GetComponent<ChronoWrist>();
            if(!Hands)Hands=player.view.gameObject.AddComponent<ChronoWrist>();
            Hands.game=this;
            Interface = gameObject.AddComponent<ChronoInterface>();
            Interface.Initialize(this);
            SetCursor();
        }
        void Update()
        {
            if (!IsPlaying) return;
            elapsed += Time.deltaTime;
            rotor.Tick(Time.deltaTime); reactor.Tick(Time.deltaTime); bridge.Tick(Time.deltaTime);
            if (reactor.secured) powerDoor.position = Vector3.MoveTowards(powerDoor.position, new Vector3(0, 6, 27), Time.deltaTime * 4);
            if (bridge.secured) exitDoor.position = Vector3.MoveTowards(exitDoor.position, new Vector3(0, 6, 48), Time.deltaTime * 4);
            Vector3 p = player.transform.position;
            if (p.y < -3) { Recover("추락했습니다. 체크포인트에서 재시도하세요."); return; }
            if (!rotorCleared && p.z > 8.7f && p.z < 11.5f && rotor.Mode != TemporalMode.Freeze)
            { Recover("회전 구역 위험! 장치를 정지한 상태로 통과하세요."); return; }
            if (!rotorCleared && p.z >= 11.5f)
            {
                rotorCleared = true; rotor.Secure();
                if (active == rotor) active = null;
                checkpoint = new Vector3(0, 1, 14);
                Notify("통과 완료. 안전 잠금이 회전 장치를 고정했습니다.");
                if (feedback) feedback.Play(ChronoCue.Confirm);
            }
            if (reactor.secured && p.z > 28 && p.z < 31) checkpoint = new Vector3(0, 1, 29);
            if (bridge.secured && p.z > 39) checkpoint = new Vector3(0, 1, 41);
            target = null;
            if (Physics.Raycast(player.view.transform.position, player.view.transform.forward, out var hit, 9))
                target = hit.collider.GetComponentInParent<TemporalDevice>();
        }
        public void HandleInput(Keyboard k, Mouse m)
        {
            if (!IsPlaying) return;
            if (!InputReady) return;
            if (k.f1Key.wasPressedThisFrame) { Interface.ShowControls(); return; }
            if (k.hKey.wasPressedThisFrame) { Interface.ShowHints(); return; }
            if (k.jKey.wasPressedThisFrame) { Interface.ShowJournal(); return; }
            // Refresh after this frame's mouse look; a click must use the current aim.
            target = null;
            if (Physics.Raycast(player.view.transform.position, player.view.transform.forward, out var aimHit, 9))
                target = aimHit.collider.GetComponentInParent<TemporalDevice>();
            if (k.digit1Key.wasPressedThisFrame) selected = TemporalMode.Freeze;
            if (k.digit2Key.wasPressedThisFrame) selected = TemporalMode.Accelerate;
            if (k.digit3Key.wasPressedThisFrame) selected = TemporalMode.Rewind;
            if (m.rightButton.wasPressedThisFrame) Release();
            if (m.leftButton.wasPressedThisFrame) ApplyToTarget(target, selected);
            if (k.eKey.wasPressedThisFrame) TryConfirmTarget(target);
        }
        public bool ApplyToTarget(TemporalDevice device, TemporalMode mode)
        {
            if (!IsPlaying) return false;
            if (!device || device.secured || (device != rotor && device != reactor && device != bridge) || (mode != TemporalMode.Freeze && mode != TemporalMode.Accelerate && mode != TemporalMode.Rewind))
            { Notify("9m 이내의 시간 조작 장치를 조준하세요."); if (feedback) feedback.Play(ChronoCue.Error); return false; }
            Release(); selected = mode; active = device; active.SetMode(mode);
            Notify(device.displayName + " · " + ModeName(mode));
            if (feedback) feedback.Play(mode == TemporalMode.Freeze ? ChronoCue.Freeze : mode == TemporalMode.Accelerate ? ChronoCue.Accelerate : ChronoCue.Rewind);
            return true;
        }
        public bool TryConfirmTarget(TemporalDevice device)
        {
            if (!IsPlaying) return false;
            if (device == reactor && reactor.Ready && !reactor.secured && rotorCleared)
            { reactor.Secure(); if (active == reactor) active = null; Notify("전력 공급 확정 · 다음 구역 개방"); }
            else if (device == bridge && bridge.Ready && !bridge.secured && reactor.secured)
            { bridge.Secure(); if (active == bridge) active = null; Notify("다리 복구 확정 · 에어록으로 이동"); }
            else if (player.transform.position.z > 45 && rotorCleared && reactor.secured && bridge.secured)
            {
                Completed = true; Release(); SetCursor();
                float best = PlayerPrefs.GetFloat("ChronoLock.BestTime.v1", 0);
                if (best <= 0 || elapsed < best) { PlayerPrefs.SetFloat("ChronoLock.BestTime.v1", elapsed); PlayerPrefs.Save(); }
                Interface?.ShowCompleted();
            }
            else { Notify("충전 / 복구 완료 후 장치를 조준하고 E를 누르세요."); if (feedback) feedback.Play(ChronoCue.Error); return false; }
            if (feedback) feedback.Play(Completed ? ChronoCue.Complete : ChronoCue.Confirm);
            return true;
        }
        void Release() { if (active) active.SetMode(TemporalMode.Normal); active = null; }
        void Recover(string message) { Release(); target = null; player.Teleport(checkpoint); Notify(message); if (feedback) feedback.Play(ChronoCue.Error); }
        void Notify(string message) { notice = message; noticeUntil = elapsed + 5; }
        public void TogglePause() { if (!HasStarted || Completed) return; if (Paused) Resume(); else Pause(); }
        public void Pause() { if (!HasStarted || Paused || Completed) return; Paused = true; SetCursor(); Interface?.ShowPause(); }
        public void Resume()
        {
            if (!HasStarted || Completed) return;
            Paused = false; inputReadyFrame = Time.frameCount + 1; SetCursor();
            if(Opening && Opening.Running)Interface?.ShowAwakening();else Interface?.ShowHUD();
        }
        public void ReturnToTitle()
        {
            Paused = true; SetCursor(); Interface?.ShowTitle();
        }
        public void RetryCheckpoint()
        {
            if (!HasStarted || Completed) return;
            if(Opening && Opening.Running)Opening.ResetForRun();
            Release(); target = null; player.Teleport(checkpoint);
            Notify("체크포인트로 돌아왔습니다. 복구한 장치는 유지됩니다."); Resume();
        }
        public void PlayMenuCue() { if (feedback) feedback.Play(ChronoCue.Confirm); }
        public void RefreshCursor(){SetCursor();}
        public void BeginStory(){Restart();if(Opening && Opening.HasRoom)Opening.Begin();}
        void SetCursor() { Cursor.lockState = !IsPlaying ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = !IsPlaying; }
        public void Restart()
        {
            if(Opening)Opening.ResetForRun();
            Release(); rotor.ResetDevice(); reactor.ResetDevice(); bridge.ResetDevice();
            rotorCleared = false; Completed = false; Paused = false; HasStarted = true; elapsed = 0;
            target = null; selected = TemporalMode.Freeze; inputReadyFrame = Time.frameCount + 1;
            powerDoor.position = new Vector3(0, 2, 27); exitDoor.position = new Vector3(0, 2, 48);
            checkpoint = Opening && Opening.HasRoom ? Opening.StandingPosition : new Vector3(0, 1, 3); player.Teleport(checkpoint);
            Notify("F1 조작 안내 · H 단계별 힌트 · J 발견 기록"); SetCursor(); Interface?.ResetHints(); Interface?.ShowHUD();
        }
        public static string ModeName(TemporalMode m) => m == TemporalMode.Freeze ? "정지" : m == TemporalMode.Accelerate ? "가속 ×4" : m == TemporalMode.Rewind ? "되감기 ×2" : "정상";
    }
}
