코드를 라인 단위로 검토했습니다 (FBX 트랜스폼 보존/스케일링 로직, 재질 색상·텍스처 매핑, 씬 격리, import/build/capture 실패 조건 중심).

**결론: 요청하신 범주(FBX 트랜스폼 손상, 텍스처/색상 구분 파괴, 무관한 씬 포함, import/build/capture 실패)에 해당하는 구체적 버그는 발견되지 않았습니다.**

확인한 주요 지점과 이상 없음 판단 근거:

- **루트 축 보정 보존**: `instance.transform.SetParent(actor.transform,false)` 직후 pos/rot/scale를 재대입하는 부분은 중복 코드이지만, `actor`가 이 시점에 항상 identity(새로 생성된 GameObject)이므로 값 손실이나 회전/스케일 오염은 발생하지 않습니다. Blender 축 보정 회전은 그대로 유지됩니다.
- **높이 정규화**: `before.size.y` → 스케일 계산 → `fitted` bounds로 위치 보정 순서가 올바르며, `actor`의 회전은 전혀 건드리지 않으므로 트랜스폼 왜곡 조건이 없습니다.
- **재질/텍스처 처리**: `SourceMaterial`은 원본 재질의 GUID+localId를 키로 사용해 카테고리(Skin/Suit.../Eyes/Glyph)별이 아니라 **개별 원본 재질별로** 별도 `.mat` 에셋을 생성하므로, 서로 다른 소스 텍스처/컬러가 하나로 뭉개지는 경우가 없습니다. `_BaseMap`/`_MainTex` 중 실제 텍스처가 있는 쪽만 선택해 tiling/offset까지 정확히 복사합니다. `Names`/`Colors` 배열 인덱스도 1:1로 정확히 대응됩니다.
- **씬 격리**: `Build()`는 오직 `PreviewPath`와 일치하는 씬만 닫고, ChronoLab.unity 등 다른 씬은 로드/저장/수정하지 않습니다. `CapturePreview()`에서 다른 씬의 라이트 컬링마스크를 임시로 건드리는 부분도 `finally`에서 100% 복원되며 다른 씬을 저장하지 않습니다.
- **실패 조건들**(`before.size.y<.01f`, 높이/접지 허용오차 0.006f, submesh/material 수 검증, rig bone null 검증 등)은 정상적인 리깅된 전신 성인 남성 캐릭터 기준으로 오탐(false trigger)할 조건이 없습니다.

추가로 언급할 만한 것이 있다면, `CapturePreview()`에서 `camera.aspect`를 캡처 후 `oldAspect` 값으로 되돌리지만 `ResetAspect()`를 호출하지 않아 이후 카메라가 수동 종횡비로 고정될 수 있는데, 이는 캡처 자체의 실패나 자산 손상을 유발하지 않는 부수 효과라 보고 대상에서 제외했습니다.

---

## 후속 실행 검증 메모 · 통합 담당 확인

위 내용은 당시 Claude의 정적 검토 원문입니다. 이후 실제 배치 실행에서 통합 담당이 **초기 빈 씬 처리 오류**를 발견해 가져오기 코드를 수정했습니다. 수정 후 검증 프로젝트에서 `CHRONO_CHARACTER_IMPORT_PASSED`가 기록되었으며 Generic 리그, 높이 1.7800m의 프리팹과 전용 미리보기 씬 생성을 확인했습니다.

정적 검토에서 문제를 찾지 못했다는 판단이 실행 오류가 없다는 보장은 아니었습니다. 이 후속 통과는 캐릭터 가져오기 검증이며, 기존 게임의 1인칭 손·시작 연출 연결이나 새 실행판 배포 완료를 뜻하지 않습니다.