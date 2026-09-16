코드를 라인 단위로 검토했습니다. 요청하신 대로 브릿지 히스토리 유지, 리액터 상시 충전, Mouse.delta 무배율, IMGUI 균일 스케일은 의도된 단순화로 보고 버그로 세지 않았습니다. 실제 런타임 블로커로 확신할 수 있는 항목은 아래 2건입니다.

**1) 높음 — `ChronoPlayer.cs` / `OnApplicationFocus(bool focus)`**
```csharp
void OnApplicationFocus(bool focus) { if (!focus && game && !game.Paused && !game.Completed) game.TogglePause(); }
```
게임 창(OS 포커스)이 벗어나면 자동으로 `TogglePause()`가 호출됩니다. `Paused`가 켜지면 `ChronoPlayer.Update()`와 `ChronoGame.Update()`/`HandleInput()` 모두 조건문 최상단에서 즉시 `return`하므로, 스크립트화된 Play Mode 플로우 테스트가 테스트 러너/콘솔 창 등으로 포커스를 잃는 순간 이동·마우스룩·E 확정 입력이 전부 무시되며 예외 없이 조용히 멈춥니다. 지금 진행 중인 자동화 테스트가 "입력에 반응이 없다"는 식으로 막힌다면 가장 먼저 의심할 지점입니다.

**2) 중간 — `ChronoGame.cs` / `Update()` 회전 장치 통과 분기 (vs `TryConfirmTarget`)**
```csharp
if (!rotorCleared && p.z >= 11.5f)
{
    rotorCleared = true; rotor.Secure();
    ...
}
```
리액터·브릿지를 `Secure()`할 때는 `TryConfirmTarget` 안에서 `if (active == reactor) active = null;` / `if (active == bridge) active = null;`로 `active` 참조를 정리하지만, 로터가 통과 지점(z≥11.5)에서 자동으로 `Secure()`될 때는 이 정리가 빠져 있습니다. 로터에 능력을 건 상태(`active == rotor`)로 통과하면 `active`가 계속 남아 HUD에 "연결: 회전 장치 · 우클릭 해제"가 계속 표시되고, 테스트가 통과 직후 `game.ActiveDevice == null`을 기대해 검증한다면 실패합니다(실제 게임플레이에 치명적이진 않음 — `TemporalDevice.SetMode`가 `secured` 상태에서는 강제로 Normal 처리하므로 로터 동작 자체는 안전합니다).

이 외 코드에서 예외를 던지거나 진행을 영구적으로 막는 지점은 발견하지 못했습니다.
