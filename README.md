# CHRONO STATION

시간 능력으로 우주정거장을 탈출하는 Unity 3D 1인칭 퍼즐 데모입니다. 주인공은 낯선 외계 정거장에서 깨어나 오른손 문양의 힘을 익히고 귀환 경로를 복구합니다.

## 게임 다운로드

**[최신 Windows 실행판 다운로드](https://github.com/M1nions99/chrono-lock-playtest/releases/latest/download/CHRONO_STATION-Windows.zip)**

ZIP을 폴더째 압축 해제한 뒤 `CHRONO_STATION.exe`를 실행하세요. 함께 있는 데이터 폴더와 DLL이 필요합니다.

| 입력 | 동작 |
|---|---|
| WASD / 마우스 | 이동 / 시점 |
| Space | 점프 |
| 휠 / 1~4 | 가속·감속·정지·되감기 선택 |
| 우클릭 | 자기 자신에게 사용 |
| 좌클릭 | 조준한 사물에 사용 |
| Q | 자기·사물 효과 모두 해제 |
| E / H / Esc | 상호작용 / 도움말 / 일시정지 |

같은 대상에 같은 능력을 다시 사용하면 해제됩니다. 처음 네 구역에서 자기 능력을 익히고 이후 사물 조작을 해금합니다. 감속 점프는 높이를 유지하면서 체공 시간을 늘립니다.

## Unity 프로젝트 열기

1. 저장소를 복제하거나 **Code → Download ZIP**으로 받아 압축을 풉니다.
2. Unity Hub에서 `Assets`, `Packages`, `ProjectSettings`가 있는 이 폴더를 추가합니다.
3. **Unity 6000.3.23f1**로 엽니다. 첫 실행에는 패키지 다운로드와 에셋 가져오기가 필요합니다.
4. `Assets/ChronoStation/Scenes/ChronoStationDemo.unity`를 열어 Play를 누릅니다. Unity 메뉴 **CHRONO STATION → Open Revised Demo**로도 열 수 있습니다.

`ArtSource/Blender`에는 주인공과 배경의 Blender 원본을 함께 보관합니다. `Assets/ChronoLock`에는 현재 게임에서 사용하는 공용 코드와 에셋이 있으므로 삭제하지 마세요. 캐릭터 원본 출처는 [제작 원본 안내](ArtSource/Blender/Character/Character_Source_License.md)를 참고하세요.

## 현재 제작 범위

자기 가속·감속·정지·되감기 학습부터 발전기, 이동 발판, 환풍기, 연결 발판 복구까지 **8개 구역**을 플레이할 수 있습니다. 메인 메뉴, 일시정지, 설정 저장, 단계별 도움말이 포함됩니다. 게임 종료 후 진행을 불러오는 저장 기능은 아직 없습니다.

[데모 안내](Docs/ChronoStation/새기획_데모/데모_안내.md) · [조작 개선](Docs/ChronoStation/새기획_데모/조작_개선.md) · [감속 점프 수정](Docs/ChronoStation/새기획_데모/감속점프_수정.md)

2026-09-16 검증: 8개 구역 자동 완주, 입력 처리 34개 조건, 점프 15개 조건, 메뉴·설정·화면 배치 검사 통과. Windows 실행판 시작도 확인했습니다. 실제 플레이 시간과 재미는 추가 플레이테스트가 필요합니다.

## 배포 규칙

새 버전의 검증과 다운로드 확인이 끝나면 이전 실행판과 이전 Release를 정리하여 최신 배포판 하나를 유지합니다. 소스 변경 이력은 Git에 남깁니다. 실행판은 Releases에서 배포하며 Unity 캐시와 개인 설정은 저장소에 넣지 않습니다.
