# 코드 리뷰 결과 (소스 검토 기준, 실행 검증 아님)

## ✅ 확인됨 (문제 없음)
- **타이틀/일시정지/완료 시 가시성**: `ChronoWrist.LateUpdate`의 `openingVisible`/`playing` 조건은 `game.IsPlaying`(=`HasStarted && !Paused && !Completed && !Opening.Running`)과 `Interface.Page`를 함께 검사하므로, `Pause()`, `ReturnToTitle()`, `Completed=true` 각각에서 팔이 정상적으로 숨겨집니다. 별도 결함 없음.
- 오버레이 카메라 스택 등록/해제, cullingMask 복원, `OnDestroy` 순서는 정상.

## 🐛 실제 결함

### 1. 오프닝 스킵 시 카메라 자세 미복구 — `ChronoOpening.Finish()`
스페이스로 조기 스킵하면(`Elapsed<7.2`, `rise=0`) `Finish()`는 `Running=false` 후 `player.Teleport(...)`와 `Resume()`만 호출합니다. 하지만 `ApplyPose()`는 매 프레임 `camera.SetPositionAndRotation`으로 **뷰 카메라의 월드 좌표/회전을 직접 덮어쓰고** 있었고, `Finish()`는 이 "누워있는(wake)" 자세를 별도로 되돌리지 않습니다.
- Teleport가 플레이어 루트 위치만 옮기고 카메라 로컬 회전을 리셋하지 않는다면(ChronoPlayer 코드는 제공되지 않아 확정 불가 — **가정**), 스킵 직후 시야가 기립 자세가 아닌 wake pose 그대로 남는 결함이 발생합니다.
- **최소 수정**: `Finish()`에서 `game.player.view.transform.rotation`을 명시적으로 리셋하거나, `Teleport`가 시야 회전까지 초기화하는지 확인.

### 2. `ChronoOpening.LateUpdate`의 Pause 게이트 누락
```csharp
void Update(){ if(!Running || game.Paused)return; ... }
void LateUpdate(){ if(Running)ApplyPose(); }   // game.Paused 체크 없음
```
`Update()`는 일시정지 시 진행을 멈추지만 `LateUpdate()`는 `Paused` 여부와 무관하게 매 프레임 `ApplyPose()`를 재실행합니다. `Elapsed`가 멈춰있어 현재는 동일 값 재계산이라 눈에 띄는 오류는 없지만, Update/LateUpdate 게이트가 비대칭이라 향후 일시정지 중 시점 제어가 추가되면 충돌합니다.
- **최소 수정**: `void LateUpdate(){ if(Running && !game.Paused)ApplyPose(); }`

### 3. (가정, 자산 확인 필요) 엄지 구부림 축 재사용 — `ChronoWrist.Start()`
```csharp
axis = pair.Value.InverseTransformDirection(presentation.right).normalized;
```
`index/middle/ring/pinky`와 `thumb` 본에 동일한 산출식을 적용합니다. 일반적으로 엄지 중수골은 나머지 손가락 대비 축이 회전되어 있어 동일한 `presentation.right` 기반 축을 쓰면 엄지가 굽힘이 아닌 벌어짐 방향으로 회전할 가능성이 있습니다. 실제 결과는 `ProtagonistRightArm`의 본 로컬 축 배치에 달려 있어 소스만으로는 확정할 수 없음 — **에디터에서 엄지 굽힘 방향을 시각적으로 확인 권장**.

### 4. (경미, 낮은 우선순위) `Required()` 예외 시 부분 생성물 미정리 — `ChronoWrist.Start()`
필수 본이 없으면 `Required()`가 예외를 던지는데, 이 시점은 레이어 지정/콜라이더 제거/애니메이터 비활성/`SetActive(false)` **이전**입니다. 예외 발생 시 `enabled=false`도 실행되지 않아, 콜라이더·애니메이터가 살아있는 원본 팔 모델이 씬에 그대로 노출된 채 남습니다. 다만 문제에서 명시된 대로 빌더가 본을 사전 검증하므로 정상 플로우에서는 도달하지 않는 경로입니다. 방어적으로 `try/catch` 후 `Destroy(presentation.gameObject); enabled=false;` 추가를 권장하되, 우선순위는 낮음.

### 5. (경미) `armCamera.allowMSAA=true` 하드코딩
`allowHDR`는 `view.allowHDR`를 따라가지만 `allowMSAA`는 무조건 `true`로 고정되어 메인 카메라의 MSAA 설정과 어긋날 수 있습니다. 시각 품질 문제로 correctness 결함은 아님. `armCamera.allowMSAA=view.allowMSAA;`로 맞추면 일관성 개선.

---
**요약**: 타이틀/일시정지/완료 가시성 로직은 견고합니다. 가장 실질적인 결함은 **오프닝 조기 스킵 시 카메라 자세 미복구(1)**이며, 이는 `ChronoPlayer.Teleport` 구현에 따라 실제 증상 여부가 갈리므로 해당 파일 확인을 권장합니다. 나머지는 경미하거나 자산 의존적입니다.
