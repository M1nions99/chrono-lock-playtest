# ChronoLock Blender 모듈 키트

우주정거장 1인칭 방탈출의 크기와 동선을 검증할 초기 3D 부품입니다. 완성형 아트, 텍스처, 충돌체와 게임 로직은 포함하지 않습니다.

## 생성

Blender의 Scripting 작업 공간에서 `Blender_ChronoLock_Kit.py`를 열고 Object Mode에서 Run Script를 실행합니다. `ChronoLock_임의번호` 컬렉션에 새 부품이 생성됩니다. 기존 오브젝트를 삭제하거나 기존 파일을 저장하지 않습니다. 여러 번 실행하면 새 컬렉션이 추가됩니다.

Blender 좌표 1단위를 1m로 설계했습니다. 부품은 보기 편하도록 펼쳐 배치되어 있으며, 내보낼 때 각 부품의 루트 위치를 원점으로 맞춥니다. 바닥·다리는 윗면 근처, 벽·문·콘솔은 바닥 중앙, 로터는 회전 중심이 기준점입니다. Z가 위쪽입니다.

| 모듈 | 크기 / 용도 |
|---|---|
| Floor | 4 × 4m, 바닥 패널 |
| Wall | 폭 4m × 높이 3m, 벽 패널 |
| DoorFrame | 폭 3m × 높이 3m, 안쪽 폭 2m × 높이 2.6m |
| DoorLeaf | 문틀과 별개인 슬라이딩 문짝 |
| Console | 조작 패널, 화면 높이 약 1.2m |
| Rotor | 지름 약 2.74m의 초기 환기 팬; 중심을 축으로 회전 |
| Bridge | 폭 2m × 길이 6m, 양쪽 난간 |

## FBX와 새 Blender 파일 내보내기

명령줄에서 **출력 폴더를 명시했을 때만** 파일을 내보냅니다. 다음은 이 PC에서 확인한 Blender 경로이며, 현재 열린 Blender와 별개인 백그라운드 프로세스로 실행합니다.

```powershell
& 'C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe' --background --factory-startup --python 'C:\Users\bhb41\Documents\Codex\2026-09-14\sf\outputs\Blender_ChronoLock_Kit.py' -- --output-dir 'C:\Users\bhb41\Documents\Codex\2026-09-14\sf\outputs\BlenderKit'
```

지정한 폴더 아래에 고유한 하위 폴더를 만들고 부품별 FBX 7개와 `.blend` 사본을 저장합니다. 기존 이름을 덮어쓰지 않으며, FBX에는 이번에 만든 해당 부품만 포함됩니다. `.blend` 사본에는 현재 장면의 기존 오브젝트도 포함되므로 새 빈 장면에서 실행하는 것을 권장합니다. 실행 중이던 파일의 저장 경로는 바꾸지 않습니다.

## Unity 적용

FBX를 프로젝트의 `Assets/Art/ChronoLock`에 넣습니다. Scale Factor 1에서 Floor 폭이 4 Unity 단위인지 우선 확인합니다. 부품 루트를 프리팹으로 만들고 벽·바닥·문에 단순 Box Collider를 설정합니다. 문틀은 양쪽 기둥과 상단에 각각 충돌체를 넣어 통로를 비워 둡니다. 로터는 중앙 루트의 Unity 로컬 Z축을 기준으로 회전 방향을 확인합니다. 색상은 임시 재질이며 URP에서 필요하면 새 재질을 지정합니다. 실제 조명·발광은 Unity에서 구성합니다.

## 확인 상태

2026-09-14에 Blender 5.2.1 LTS의 별도 백그라운드 프로세스로 실행해 FBX 7개와 `.blend` 저장에 성공했습니다. 저장된 `.blend`를 다시 열어 루트 7개, 메시 32개, 기본 Cube 보존, 바닥 폭·길이 4m를 확인했습니다. 바닥 FBX 재가져오기에서도 해당 루트와 메시 5개만 포함되며 폭·길이 4m가 유지됨을 확인했습니다. Unity 가져오기는 아직 검증하지 않았습니다.

검증한 결과 파일은 `BlenderKit/ChronoLock_68c9d583/`에 있습니다. `.blend` 파일명은 `ChronoLock_68c9d583.blend`입니다. 홈 디렉터리의 썸네일 캐시 쓰기 경고가 발생했지만 본 파일 저장과 재열기는 정상 완료했습니다.

FBX 내보내기 API의 선택 오브젝트, 축, 스케일 인자는 [Blender 공식 API](https://docs.blender.org/api/main/bpy.ops.export_scene.html)를 확인했습니다. `.blend` 저장의 copy 옵션은 [공식 저장 API 설명](https://docs.blender.org/api/blender_python_api_2_69_1/bpy.ops.wm.html)을 참고했습니다. 다른 PC에서 FBX 내보내기 기능이 비활성화되어 있으면 먼저 활성화해야 합니다.
