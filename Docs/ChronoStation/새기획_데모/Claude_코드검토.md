## 발견한 버그: Stage 7(Cargo) 되감기 이력이 Stop 사용 중 자기 자신을 덮어써서 훼손됨

### 정확한 발생 조건
`StationTemporalObject.Tick()`에서 매 30Hz 샘플마다 기록 여부를 판단하는 로직입니다.

```csharp
bool cargoAlreadyArrived = kind == StationDeviceKind.Cargo && parameter >= 1;
Vector3 beforePosition = movingPart.localPosition;
...
if (multiplier > 0) Advance(SampleStep * multiplier);
if (cargoAlreadyArrived && beforePosition.Equals(movingPart.localPosition) ... ) continue;
Record();
```

`Stop` 능력은 `multiplier = 0`이므로 `Advance()` 자체가 호출되지 않아 트랜스폼이 전혀 변하지 않습니다. 이 "값이 변하지 않은 샘플"을 기록에서 제외하는 조건은 **오직 `cargoAlreadyArrived`(파라미터가 이미 1에 도달해 종착점에서 대기 중)일 때만** 적용됩니다.

문제는 08번 구역의 이탈 발판(`kind = Cargo`, `pingPong = false`)이 **기본 상태에서도 자동으로 계속 움직인다**는 점입니다(`ActiveAbility == null`이면 `multiplier`는 else 분기로 1이 되어 항상 전진). 즉 플레이어가 아무것도 하지 않아도 발판은 0→1로 이동합니다. 만약 플레이어가 이 발판이 아직 **파라미터 0과 1 사이(이동 중)**일 때 사물 대상으로 `Stop`을 걸면:

- `cargoAlreadyArrived`는 `false`(아직 도착 전이므로)
- 하지만 트랜스폼은 변하지 않음(Advance 스킵)
- 그런데도 `continue`가 걸리지 않아 **매 샘플마다 동일한(멈춘) 좌표가 그대로 `Record()`됨**

`history`는 `HistoryCapacity = 361`(약 12초 분량)짜리 원형 버퍼이고, 가득 차면 `history.RemoveAt(0)`으로 **가장 오래된 샘플부터** 지웁니다. 이 가장 오래된 샘플에는 도킹 위치(파라미터 ≈ 0)에서 기록된 최초 스냅샷(`ResetDevice()`에서 `Record()`한 것)이 포함되어 있습니다. 발판이 어느 정도 이동한 뒤(예: 3초 진행, param≈0.5) 플레이어가 Stop을 걸고 약 9~10초 이상 유지하면(총 약 12초 실시간), 버퍼가 가득 차면서 **가장 먼저 제거되는 것이 바로 이 도킹 위치 스냅샷**입니다. 이후 계속 Stop을 유지할수록 정지된 좌표의 중복 샘플이 실제 이동 기록을 앞에서부터 밀어내며 잠식합니다.

### 결과적으로 발생하는 문제
08번 구역 클리어 조건은 다음과 같습니다.

```csharp
if (CurrentStage.device.Progress <= .015f && CurrentStage.device.HasUsed(StationAbility.Rewind) && stageTime > 1)
    { CurrentStage.device.Secure(); Solve(...); }
```

`Rewind`는 `history`를 뒤에서부터 하나씩 pop하며 되감는데, 앞서 도킹(파라미터≈0) 스냅샷이 이미 지워졌다면 아무리 되감아도 `history.Count == 1`에 도달했을 때 남는 것은 "정지된 채 파라미터가 0이 아닌" 좌표입니다. 즉 `Progress`가 `.015f` 이하로 절대 내려가지 않아 **08번(최종) 구역의 클리어 조건이 영구히 성립하지 않는** 진행 불가 상태가 됩니다. 이것은 "실제 기록만 되감는다"는 규칙 자체를 어기는 것이며(가짜 과거를 만드는 게 아니라 진짜 과거를 지워버리는 형태), 플레이어 입장에서는 이유를 알 수 없는 소프트락으로 체감됩니다.

(참고: `Retry()`가 `ResetDevice()`를 다시 호출해 `history`를 초기화하므로, 구덩이에 빠지는 등으로 스테이지를 강제로 재시작하면 복구는 됩니다만, 이는 플레이어가 원인을 모른 채 우연히 회피하는 것일 뿐 정상적인 해법이 아닙니다.)

### 최소 수정안
"변화 없는 샘플은 기록하지 않는다"는 스킵 조건을 Cargo 도착 시점에만 한정하지 말고, **모든 종류·모든 상황에서 트랜스폼이 실제로 변하지 않았을 때 공통 적용**하도록 한 줄만 고치면 됩니다.

```csharp
// 기존
if (cargoAlreadyArrived && beforePosition.Equals(movingPart.localPosition)
    && beforeRotation.Equals(movingPart.localRotation)
    && beforeScale.Equals(movingPart.localScale)) continue;

// 수정
if (beforePosition.Equals(movingPart.localPosition)
    && beforeRotation.Equals(movingPart.localRotation)
    && beforeScale.Equals(movingPart.localScale)) continue;
```

`cargoAlreadyArrived` 변수와 `kind == Cargo` 분기 자체는 그대로 두되(주석에서 말한 "Cargo 종착점 대기 상태 제외" 의도는 유지됨), 판별 조건에서 `kind` 제약만 제거하면 Stop으로 인해 정지된 모든 상황(어떤 장치든)에서 동일 좌표가 반복 기록되어 실제 이동 이력을 밀어내는 문제가 사라집니다. 이 변경은 "진짜 기록만 남긴다"는 클래스의 설계 의도와도 정확히 부합하며, 다른 스테이지(플랫폼·환풍기)는 애초에 되감기를 요구하지 않으므로 동작에 영향이 없습니다.

### 그 외 검토
자기 능력 4종 선해금 순서, `Update`(order 20) 이후 `StationPlayer.Update`(order 40)에서 플레이어 이동을 처리하는 구조, `Cargo`가 종착점에서 대기할 때 이력을 남기지 않는 처리, 테스트용 `AutomatedInput`/`TestMove`/`TestJump` 프로퍼티는 모두 의도된 설계로 확인되어 문제로 보지 않았습니다. 다른 스테이지(0~6번)의 해금·게이트·판정 로직은 상호 정합성이 맞아 진행을 막는 결함을 찾지 못했습니다.
