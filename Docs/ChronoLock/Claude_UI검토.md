# UGUI 메뉴/설정 통합 리뷰 결과

## 1. UI 입력 모듈 누락 위험 — `ChronoInterface.Initialize()`
`if (!EventSystem.current)`만 확인하고 실제로 `InputSystemUIInputModule`이 붙어있는지는 확인하지 않습니다. 씬에 구식 `StandaloneInputModule`을 쓰는 EventSystem이 이미 존재하면(다른 씬/프리팹에서 흔함), 새 Input System 기반 프로젝트에서 마우스 클릭·Tab 네비게이션이 전혀 동작하지 않는 무음 실패가 발생합니다.
- **수정**: `EventSystem.current?.GetComponent<InputSystemUIInputModule>()`가 null이면 컴포넌트를 추가하도록 조건 변경.

## 2. 렌더 파이프라인 세이프가드 비대칭 — `ChronoPreferences.ApplyQuality()`
URP 분기는 `if (!Application.isPlaying) return;`로 에디터 모드에서 애셋 보호를 하지만, Built-in RP(else) 분기는 동일 가드가 없어 `QualitySettings.shadowDistance/shadowResolution/antiAliasing`을 플레이 모드 밖에서도 직접 수정할 수 있습니다. "authored asset을 건드리지 않는다"는 클래스 자체 설계 의도와 어긋나며, Built-in RP 프로젝트에서는 에디터의 프로젝트 설정을 영구 오염시킬 수 있습니다.
- **수정**: else 분기 앞에도 `if (!Application.isPlaying) return;` 추가.

## 3. Controls origin 캡처 순서 버그 — `ChronoInterface.ShowControls()`
`controlsOrigin=Page;`가 `game.Pause()` 호출 **이후**에 실행됩니다. `Pause()`는 내부적으로 `Interface.ShowPause()`를 호출해 `Page`를 이미 `Pause`로 바꿔버리므로, HUD(F1)에서 들어온 origin이 유실되고 항상 Pause로 캡처됩니다. 현재는 `Return()`의 catch-all(`else ShowPause()`)에 가려져 있지만, 향후 HUD 직행 로직을 추가하면 즉시 표면화되는 잠재 결함입니다.
- **수정**: `controlsOrigin=Page;`를 `game.Pause()` 호출 전으로 이동.

## 4. 조준 레이캐스트에 레이어 마스크 없음 — `ChronoGame.Update()` / `HandleInput()`
`Physics.Raycast(player.view.transform.position, player.view.transform.forward, out var hit, 9)`에 `LayerMask`가 없습니다. 카메라는 플레이어 캡슐 콜라이더 내부/근접 위치에 있어, 자기 자신의 콜라이더나 씬의 장식용/트리거 콜라이더가 레이에 걸리면 원거리 장치 조준(`target`)이 무작위로 막힐 수 있습니다.
- **수정**: 장치 전용 `LayerMask`를 raycast 마지막 인자로 전달.

**요약**: EventSystem/입력 모듈 확인 로직 강화, Built-in RP 분기 Play-mode 가드 추가, `controlsOrigin` 캡처 위치 이동, 조준 레이캐스트에 레이어 마스크 적용을 권장합니다.
