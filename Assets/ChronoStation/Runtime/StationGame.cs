using System.Collections.Generic;
using ChronoLock;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChronoStation
{
    [DefaultExecutionOrder(20)]
    public sealed class StationGame : MonoBehaviour
    {
        public StationPlayer player;
        public StationStage[] stages;
        public StationPlayer Player => player;
        public StationPage Page { get; private set; } = StationPage.Title;
        public bool HasStarted { get; private set; }
        public bool Opening { get; private set; }
        public float OpeningElapsed { get; private set; }
        public bool IsPlaying => HasStarted && !Opening && Page == StationPage.HUD;
        public bool AcceptsInput => Time.frameCount > inputBlockedUntil;
        public bool ObjectUnlocked => Step >= 4;
        public int UnlockedAbilities => Mathf.Min(Step + 1, 4);
        public int Step { get; private set; }
        public int Attempts { get; private set; }
        public float Elapsed { get; private set; }
        public StationAbility Selected { get; private set; }
        public StationAbility? SelfActive { get; private set; }
        public float SelfRemaining { get; private set; }
        public float Cooldown { get; private set; }
        public bool Rewinding { get; private set; }
        public StationTemporalObject ActiveObject { get; private set; }
        public StationTemporalObject Focused { get; private set; }
        public int CastSerial { get; private set; }
        public bool StageSolved { get; private set; }
        public bool MemoryFound { get; private set; }
        public bool SequenceRunning => sequence;
        public float SequenceTime => sequenceTime;
        public StationStage CurrentStage => stages[Step];
        public float StepProgress => StageSolved ? 1 : Step == 4 && CurrentStage.device ? CurrentStage.device.Progress : sequence ? Mathf.Clamp01(sequenceTime / (Step == 2 ? 3 : 3.2f)) : 0;
        public string StepTitle => titles[Step];
        public string Objective => objectives[Step];
        public string Hint => hints[Step];
        public string Status => messageTime > 0 ? message : DefaultStatus();
        public string UsePrompt
        {
            get
            {
                if (Opening) return "[Enter] 깨어나기 건너뛰기";
                if (!IsPlaying) return "";
                if (Near(CurrentStage.exit, 2.5f)) return StageSolved ? "[E] " + (Step == 7 ? "지구행 귀환선 출발" : "다음 구역으로") : "출구 잠김 · " + Objective;
                if (ObjectUnlocked && Focused) return "[좌클릭] " + Focused.displayName + " · " + (Focused.ActiveAbility == Selected ? "해제" : AbilityName(Selected));
                if (Near(CurrentStage.terminal, 2.8f)) return "[E] " + terminalPrompts[Step];
                return "[우클릭] 자기 " + (SelfActive == Selected || SelfActive == StationAbility.Stop ? "능력 해제" : AbilityName(Selected));
            }
        }
        static readonly string[] titles = { "01 / 격리 연구실 · 가속", "02 / 격리 연구실 · 감속", "03 / 격리 연구실 · 정지", "04 / 격리 연구실 · 되감기", "05 / 정비 통로 · 전력 복구", "06 / 정비 통로 · 이동 발판", "07 / 도킹 구역 · 환기 장치", "08 / 도킹 구역 · 귀환선" };
        static readonly string[] objectives = {
            "자기 가속으로 닫히기 전 방화문을 통과하세요.",
            "자기 감속으로 체공 시간을 늘려 틈을 건너세요.",
            "검사대에서 자기 정지로 에너지 충격을 막으세요.",
            "관측 기록을 회수한 뒤 자기 되감기로 입구에 돌아오세요.",
            "사물 조작을 해금했습니다. 발전기를 가속해 전력을 복구하세요.",
            "이동 발판을 감속한 뒤 올라타 정비 틈을 건너세요.",
            "환풍기를 정지시켜 도킹 통로를 통과하세요.",
            "이탈한 연결 발판의 실제 움직임을 되감아 귀환선에 도달하세요." };
        static readonly string[] hints = {
            "단말 옆에서 우클릭으로 자기 가속 → E로 문 개방 → W로 통과하세요. 제한은 3.2초입니다.",
            "우클릭으로 자기 감속, 바닥 끝에서 W + Space. 점프 높이는 같고 공중에 머무는 시간이 늘어납니다.",
            "검사대에서 E를 누르면 3초 뒤 충격이 옵니다. ‘충격 임박’에 우클릭으로 자기 정지. 정지 중엔 움직일 수 없습니다.",
            "끝의 단말에서 E로 기록을 회수하세요. 우클릭으로 자기 되감기를 쓰면 구역 입구로 돌아옵니다. 확보한 기록은 유지됩니다.",
            "발전기를 조준하고 좌클릭으로 가속하세요. 충전이 완료되면 문이 열립니다. 우클릭은 자신에게 적용됩니다.",
            "발판이나 제어기를 조준하고 좌클릭으로 감속하세요. 올라탄 뒤 Q로 해제하면 정상 속도로 이동합니다.",
            "환풍기나 옆 제어기를 조준하고 좌클릭으로 정지시키세요. 통과할 때까지 정지를 유지하세요.",
            "연결 발판이 이탈하는 과정을 관찰하세요. 발판이나 제어기를 조준하고 좌클릭으로 되감으세요. 원위치에서 자동 고정됩니다." };
        static readonly string[] terminalPrompts = { "방화문 개방 · 3.2초", "체공 훈련 안내", "방호 검사 시작", "관측 기록 회수", "사물 조작 안내", "이동 발판 안내", "환기 통로 안내", "연결 발판 기록 안내" };
        StationUI ui;
        ChronoFeedback sound;
        StationPage returnPage = StationPage.Title;
        bool sequence, abilityUsed;
        float sequenceTime, messageTime, rewindElapsed, stageTime;
        string message;
        readonly List<Vector3> trail = new List<Vector3>();
        List<Vector3> replay;
        float trailClock;
        MaterialPropertyBlock signalBlock;
        int inputBlockedUntil;

        void Start()
        {
            ChronoPreferences.Load();
            sound = gameObject.AddComponent<ChronoFeedback>();
            ui = gameObject.AddComponent<StationUI>(); ui.Initialize(this);
            foreach (var stage in stages) if (stage.device) stage.device.Initialize();
            signalBlock = new MaterialPropertyBlock();
            SelectStage(0);
            SetPage(StationPage.Title);
            player.Teleport(stages[0].transform.TransformPoint(new Vector3(-3, .05f, 1)), Quaternion.Euler(0, 20, 0));
        }
        void Update()
        {
            if (Pressed(Key.Escape))
            {
                if (Page == StationPage.HUD) Pause(); else if (Page == StationPage.Pause) Resume(); else if (Page == StationPage.Options || Page == StationPage.Controls) Back();
            }
            if (Page != StationPage.HUD || !AcceptsInput) return;
            if (Opening)
            {
                OpeningElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, OpeningElapsed / 3.5f);
                player.view.transform.localPosition = new Vector3(0, Mathf.Lerp(.62f, 1.62f, t), 0);
                player.view.transform.localRotation = Quaternion.Euler(Mathf.Lerp(38, 0, t), 0, Mathf.Lerp(-13, 0, t));
                messageTime = 1;
                message = OpeningElapsed < 2.6f ? "…구조 신호는 끊겼다. 이곳은 지구의 정거장이 아니다." : OpeningElapsed < 5.4f ? "오른손에 남은 문양. 주위가 아니라, 내 시간이 먼저 반응한다." : "정거장 안내: 격리 해제를 위해 네 가지 시간 반응을 동기화하십시오.";
                if (OpeningElapsed >= 9 || Pressed(Key.Enter)) SkipOpening();
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            Elapsed += dt; stageTime += dt; messageTime -= dt;
            if (Rewinding) { TickRewind(dt); return; }
            Cooldown = Mathf.Max(0, Cooldown - dt);
            if (SelfActive.HasValue)
            {
                SelfRemaining -= dt;
                if (SelfRemaining <= 0) ReleaseSelf();
            }
            if (CurrentStage.device) CurrentStage.device.Tick(dt);
            Physics.SyncTransforms();
            if (ActiveObject && !ActiveObject.ActiveAbility.HasValue) ActiveObject = null;
            Focused = null;
            if (Physics.Raycast(player.View.transform.position, player.View.transform.forward, out var hit, 30, ~(1 << 30), QueryTriggerInteraction.Collide))
                Focused = hit.collider.GetComponentInParent<StationTemporalObject>();
            for (int i = 0; i < 4; i++) if (Pressed((Key)((int)Key.Digit1 + i))) SelectAbility((StationAbility)i);
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f) CycleAbility(wheel > 0 ? 1 : -1);
            }
            if (Pressed(Key.Q)) ReleaseAbility();
            else if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) UseObjectAbility();
                if (Mouse.current.rightButton.wasPressedThisFrame) UseSelfAbility();
            }
            if (Pressed(Key.E)) Interact();
            if (Pressed(Key.H)) Notify(Hint, 8);
            RecordTrail(dt);
            EvaluateStage(dt);
            if (player.transform.position.y < CurrentStage.transform.position.y - 4) Retry();
        }
        void LateUpdate()
        {
            if (CurrentStage.signal)
            {
                var color = StageSolved ? new Color(.1f, 1, .65f) : new Color(1, .42f, .08f);
                signalBlock.SetColor("_BaseColor", color); signalBlock.SetColor("_EmissionColor", color * 1.5f);
                CurrentStage.signal.SetPropertyBlock(signalBlock);
            }
        }
        public void NewGame()
        {
            HasStarted = true; Attempts = 0; Elapsed = 0; MemoryFound = false;
            SelectStage(0); Opening = true; OpeningElapsed = 0; SetPage(StationPage.HUD);
        }
        public void SkipOpening() { Opening = false; player.Teleport(CurrentStage.entry.position, CurrentStage.entry.rotation); Notify(Hint, 7); }
        public void Pause() { if (Page == StationPage.HUD) SetPage(StationPage.Pause); }
        public void Resume() { if (HasStarted && Page != StationPage.Completed) SetPage(StationPage.HUD); }
        public void ShowOptions() { returnPage = Page; SetPage(StationPage.Options); }
        public void ShowControls() { returnPage = Page; SetPage(StationPage.Controls); }
        public void Back() { SetPage(returnPage); }
        public void BackToTitle() { if (Opening) SkipOpening(); SetPage(StationPage.Title); }
        public void Quit() { Application.Quit(); }
        void SetPage(StationPage page)
        {
            Page = page;
            inputBlockedUntil = Time.frameCount + 1;
            Cursor.lockState = page == StationPage.HUD ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = page != StationPage.HUD;
            if (ui) ui.Show(page);
        }
        public void SelectAbility(StationAbility ability)
        {
            if ((int)ability >= UnlockedAbilities) { Notify("이 능력은 다음 동기화 구역에서 해금됩니다."); return; }
            Selected = ability;
        }
        public void CycleAbility(int direction)
        {
            if (direction == 0) return;
            SelectAbility((StationAbility)(((int)Selected + (direction > 0 ? 1 : -1) + UnlockedAbilities) % UnlockedAbilities));
        }
        public bool UseSelfAbility() => ApplyAbility(StationTarget.Self);
        public bool UseObjectAbility() => ApplyAbility(StationTarget.Object);
        bool ApplyAbility(StationTarget target)
        {
            if (!IsPlaying || Rewinding) return false;
            if (target == StationTarget.Object)
            {
                if (!ObjectUnlocked) { Notify("지금은 우클릭으로 자신에게 사용하세요. 사물 조작은 네 가지 동기화 후 해금됩니다."); return false; }
                if (!Focused) { Notify("좌클릭은 조준한 사물에 사용합니다. 자신에게 사용하려면 우클릭하세요."); return false; }
                if (ActiveObject == Focused && Focused.ActiveAbility == Selected)
                { ActiveObject.Release(); ActiveObject = null; Notify("사물 능력 해제"); return true; }
                if (!Focused.Apply(Selected)) { Notify("되감을 움직임이 아직 기록되지 않았거나 이미 고정된 장치입니다."); return false; }
                if (ActiveObject && ActiveObject != Focused) ActiveObject.Release();
                ActiveObject = Focused;
                CastSerial++; Cue(Selected);
                Notify(Focused.displayName + " · " + AbilityName(Selected));
                return true;
            }
            if (SelfActive == Selected || SelfActive == StationAbility.Stop)
            { ReleaseSelf(); Notify("자기 능력 해제"); return true; }
            if (Cooldown > 0) { Notify("문양 안정화 중 · " + Cooldown.ToString("0.0") + "초"); return false; }
            CastSerial++; Cue(Selected);
            if (Selected == StationAbility.Rewind) { BeginRewind(); return true; }
            SelfActive = Selected;
            SelfRemaining = Selected == StationAbility.Stop ? 2.1f : Selected == StationAbility.Slow ? 7.5f : 5;
            if (Step == (int)Selected) abilityUsed = true;
            Notify("자기 " + AbilityName(Selected) + (Selected == StationAbility.Stop ? " · 움직임 고정 / 피해 무효" : " · " + SelfRemaining.ToString("0.0") + "초"));
            return true;
        }
        public void ReleaseAbility()
        {
            if (Rewinding) return;
            bool active = SelfActive.HasValue || ActiveObject;
            if (ActiveObject) ActiveObject.Release();
            ActiveObject = null; ReleaseSelf();
            if (active) Notify("시간 능력 해제");
        }
        void ReleaseSelf() { if (SelfActive.HasValue) Cooldown = .7f; SelfActive = null; SelfRemaining = 0; }
        public void Interact()
        {
            if (!IsPlaying || Rewinding || SelfActive == StationAbility.Stop) return;
            if (Near(CurrentStage.exit, 2.5f))
            {
                if (!StageSolved) { Notify(Hint, 6); return; }
                if (Step == 7) { ReleaseAbility(); HasStarted = false; SetPage(StationPage.Completed); sound.Play(ChronoCue.Complete); return; }
                SelectStage(Step + 1); sound.Play(ChronoCue.Confirm); return;
            }
            if (!Near(CurrentStage.terminal, 2.8f)) return;
            if (Step == 0 || Step == 2)
            {
                if (StageSolved) return;
                sequence = true; sequenceTime = 0;
                Notify(Step == 0 ? "방화문 개방 · 3.2초 안에 전방 문을 통과하세요." : "방호 검사 시작 · 3초 뒤 충격. 검사대에 머무르세요.", 3);
            }
            else if (Step == 3)
            {
                MemoryFound = true; Notify("관측 기록 확보 · 지구행 귀환선이 남아 있다. 우클릭으로 자기 되감기를 써서 입구에 돌아가세요.", 7);
                sound.Play(ChronoCue.Confirm);
            }
            else Notify(Hint, 8);
        }
        void EvaluateStage(float dt)
        {
            var local = CurrentStage.Local(player.transform.position);
            if (sequence) sequenceTime += dt;
            switch (Step)
            {
                case 0:
                    SetGate(StageSolved || sequence && sequenceTime < 3.2f);
                    if (sequence && sequenceTime <= 3.2f && local.z > 21 && abilityUsed) Solve("가속 동기화 완료 · 문을 통과했습니다.");
                    else if (sequence && sequenceTime >= 3.2f && !StageSolved) RetryWith("방화문이 닫혔습니다. 단말 옆에서 가속한 뒤 다시 시도하세요.");
                    break;
                case 1:
                    if (local.z > 16 && abilityUsed) Solve("감속 동기화 완료 · 체공 시간을 늘려 통과했습니다.");
                    break;
                case 2:
                    if (sequence && !StageSolved)
                    {
                        if (Vector3.Distance(new Vector3(local.x, 0, local.z), new Vector3(0, 0, 8)) > 2.5f) { RetryWith("검사대를 벗어났습니다. 충격을 정지로 막아 동기화하세요."); break; }
                        if (sequenceTime >= 2.1f && sequenceTime < 3) Notify("충격 임박 · 지금 자기 정지를 사용하세요!", .2f);
                        if (sequenceTime >= 3)
                        {
                            if (SelfActive == StationAbility.Stop) { sequence = false; Solve("정지 동기화 완료 · 에너지 충격을 무효화했습니다."); }
                            else RetryWith("충격 방어 실패 · 세 번째 신호에 맞춰 자기 정지를 사용하세요.");
                        }
                    }
                    SetGate(StageSolved);
                    break;
                case 4:
                    if (CurrentStage.device.Progress >= .99f && CurrentStage.device.HasUsed(StationAbility.Accelerate)) Solve("전력 복구 완료 · 사물의 진행 시간을 가속했습니다.");
                    SetGate(StageSolved);
                    break;
                case 5:
                    if (local.z > 20 && CurrentStage.device.HasUsed(StationAbility.Slow)) Solve("정비 통로 통과 · 움직이는 발판의 시간을 조절했습니다.");
                    break;
                case 6:
                    bool stopped = CurrentStage.device.ActiveAbility == StationAbility.Stop;
                    SetGate(stopped || StageSolved);
                    if (!StageSolved && Mathf.Abs(local.z - 12) < .85f && !stopped) { RetryWith("환풍기가 작동 중입니다. 사물 정지로 안전 통로를 확보하세요."); break; }
                    if (local.z > 13 && stopped) Solve("환기 통로 통과 · 외부 장치 정지 동기화 완료.");
                    break;
                case 7:
                    if (!StageSolved && !CurrentStage.device.HasInitialHistory)
                        Notify("초기 움직임 기록이 소진되었습니다. 4 선택 → 우클릭으로 자기 되감기를 써서 이 구역을 다시 시도하세요.", .2f);
                    if (CurrentStage.device.Progress <= .015f && CurrentStage.device.HasUsed(StationAbility.Rewind) && stageTime > 1)
                    {
                        CurrentStage.device.Secure(); Solve("연결 발판 복구 / 고정 · 귀환선으로 이동하세요.");
                    }
                    break;
            }
        }
        void Solve(string text)
        {
            if (StageSolved) return;
            StageSolved = true; sequence = false; Notify(text, 5); sound.Play(ChronoCue.Confirm);
        }
        void SetGate(bool open)
        {
            if (!CurrentStage.gate) return;
            var position = CurrentStage.gate.localPosition;
            position.y = Mathf.MoveTowards(position.y, open ? 6 : 2.2f, Time.deltaTime * 12);
            CurrentStage.gate.localPosition = position;
        }
        void SelectStage(int index)
        {
            Step = index;
            inputBlockedUntil = Time.frameCount + 1;
            for (int i = 0; i < stages.Length; i++) stages[i].gameObject.SetActive(i == Step);
            ResetStage(false);
            Selected = (StationAbility)(Step < 4 ? Step : Step - 4);
            Notify(Step == 4 ? "사물 조작 해금 · 좌클릭은 조준한 사물 / 우클릭은 자신 / Q는 능력 해제" : Hint, 8);
        }
        void ResetStage(bool preserveMemory)
        {
            if (!preserveMemory && Step == 3) MemoryFound = false;
            SelfActive = null; SelfRemaining = 0; Cooldown = 0; ActiveObject = null; Focused = null;
            Rewinding = false; sequence = false; abilityUsed = false; StageSolved = false;
            sequenceTime = stageTime = 0; trail.Clear(); trailClock = 0;
            if (CurrentStage.device) CurrentStage.device.ResetDevice();
            if (CurrentStage.gate) { var p = CurrentStage.gate.localPosition; p.y = 2.2f; CurrentStage.gate.localPosition = p; }
            player.Teleport(CurrentStage.entry.position, CurrentStage.entry.rotation);
            trail.Add(player.transform.position);
        }
        public void Retry()
        {
            if (!HasStarted) return;
            Attempts++; Opening = false; ResetStage(true); SetPage(StationPage.HUD); Notify("현재 구역에서 다시 시작합니다. [H] 도움말", 4);
        }
        void RetryWith(string text) { Retry(); Notify(text, 6); sound.Play(ChronoCue.Error); }
        void RecordTrail(float dt)
        {
            trailClock += dt;
            if (trailClock < .075f) return;
            trailClock = 0; trail.Add(player.transform.position);
            // Keep the entrance and a bounded path for a short retry presentation.
            if (trail.Count > 1600) trail.RemoveAt(1);
        }
        void BeginRewind()
        {
            SelfActive = StationAbility.Rewind; SelfRemaining = 1.35f; Rewinding = true; rewindElapsed = 0;
            replay = new List<Vector3>(trail); replay.Add(player.transform.position); Notify("자기 되감기 · 현재 구역 입구로 복귀", 2);
        }
        void TickRewind(float dt)
        {
            rewindElapsed += dt; SelfRemaining = Mathf.Max(0, 1.35f - rewindElapsed);
            float point = (1 - Mathf.Clamp01(rewindElapsed / 1.35f)) * (replay.Count - 1);
            int i = Mathf.FloorToInt(point);
            player.Teleport(Vector3.Lerp(replay[i], replay[Mathf.Min(i + 1, replay.Count - 1)], point - i), player.transform.rotation);
            if (rewindElapsed < 1.35f) return;
            bool finishLesson = Step == 3 && MemoryFound;
            Attempts++; ResetStage(true);
            if (finishLesson) Solve("되감기 동기화 완료 · 기억은 유지됩니다. 입구의 외부 동기화 게이트를 여세요.");
            else Notify("현재 구역의 시작 상태를 복구했습니다. 능력 해금은 유지됩니다.", 4);
        }
        bool Near(Transform target, float range) => target && Vector3.Distance(player.transform.position + Vector3.up, target.position) < range;
        void Cue(StationAbility ability) { sound.Play(ability == StationAbility.Accelerate ? ChronoCue.Accelerate : ability == StationAbility.Rewind ? ChronoCue.Rewind : ChronoCue.Freeze); }
        public void Notify(string text, float seconds = 3) { message = text; messageTime = seconds; }
        string DefaultStatus()
        {
            if (Rewinding) return "현재 구역을 되감는 중";
            if (SelfActive.HasValue) return "자기 " + AbilityName(SelfActive.Value) + " · " + SelfRemaining.ToString("0.0") + "초";
            if (Cooldown > 0) return "문양 안정화 · " + Cooldown.ToString("0.0") + "초";
            if (ObjectUnlocked && Focused) return Focused.displayName + (Focused.kind == StationDeviceKind.Generator ? " · 충전 " + Mathf.RoundToInt(Focused.Progress * 100) + "%" : " · 움직임 기록 " + Focused.HistorySeconds.ToString("0.0") + "초");
            return "[H] 현재 퍼즐 도움말";
        }
        void OnApplicationFocus(bool focus) { if (!focus && !Application.isBatchMode && Page == StationPage.HUD) Pause(); }
        void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public static string AbilityName(StationAbility ability) => ability == StationAbility.Accelerate ? "가속" : ability == StationAbility.Slow ? "감속" : ability == StationAbility.Stop ? "정지" : "되감기";
        static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }
}
