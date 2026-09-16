# Blender MCP 설치·연결 완료

확인일: 2026-09-14

Blender MCP를 설치하고, 실행 중인 Blender 5.2와 실제 통신까지 검증했습니다. 우주정거장 맵 모델링에 활용할 수 있습니다.

## 검증 결과
- 기존 MCP 실행 파일의 손실된 독립 Python 환경 복구 완료.
- Blender 사용자 애드온 설치·활성화 완료. localhost:9876 서버 실행 중.
- MCP initialize 및 tools/list 성공: 28개 도구 확인.
- get_scene_info 성공: 현재 Scene에서 Cube, Light, Camera 총 3개 오브젝트 반환.
- execute_blender_code 성공: 기존 의도대로 원격측정 동의를 False로 설정하고 사용자 환경설정만 저장.
- 검증 클라이언트는 BLENDER_MCP_DISABLE_TELEMETRY=1을 적용.
- 사용자 Blender 씬의 오브젝트와 파일은 변경하거나 저장하지 않았습니다.

## 설치 내역
- 패키지: blender-mcp 1.9.1, MCP Python SDK 1.30.0.
- 기존 실행 파일: C:\Users\bhb41\.local\bin\blender-mcp.exe
- 독립 환경: C:\Users\bhb41\AppData\Roaming\uv\tools\blender-mcp
- 애드온: C:\Users\bhb41\AppData\Roaming\Blender Foundation\Blender\5.2\scripts\addons\blender_mcp.py
- 독립 환경은 기존 Blender 내장 Python을 기반으로 생성했습니다. Blender 내장 라이브러리에 추가 패키지를 설치하지 않았습니다.
- 공식 저장소 commit: 5f8ddaf6e987c4aa0c3467fcc548838b28f64477.
- 검토 소스와 설치 애드온의 SHA256 일치: F43469C8518C7021E0060E32CFE52E3BEB126B0F62FBAE7293106642A3EBDA89.
- Codex 전역 설정은 변경하지 않았습니다.

## 사용 시 참고
현재 대화의 기본 도구 목록에는 Blender MCP가 새로 나타나지 않았지만, 표준 MCP 클라이언트를 통해 실제 씬 조회와 코드 실행이 검증되었습니다. Codex를 다시 시작하면 기존 설정의 기본 도구 연결을 다시 시도할 수 있습니다. 현재 연결 자체는 사용 가능합니다.

애드온은 활성화 시 서버를 자동 시작합니다. Blender를 종료하면 연결이 끊기므로 맵 작업 시 Blender를 열어 두어야 합니다.

uv 설치의 마지막 실행 파일 덮어쓰기는 기존 프로세스 잠금으로 실패했지만, 기존 실행 파일이 복구된 환경으로 정상 작동함을 독립적으로 검증했습니다.

출처: https://github.com/ahujasid/blender-mcp

## 실제 게임 모델 미리보기
생성한 PolishedBlenderAssets/ChronoLock_HeroAssets_Final.blend의 Scene을 라이브러리 append로 별도 CHRONO_LOCK_Asset_Preview 씬에 불러왔습니다. Blender 현재 창은 새 씬의 카메라 구도로 표시하고 재질 미리보기를 켰습니다.

MCP get_scene_info 재검증 결과: CHRONO_LOCK_Asset_Preview, 오브젝트 117개, 재질 데이터블록 8개. 기존 사용자 Scene의 Cube·Light·Camera 3개는 유지했고 원본 씬이나 파일을 저장하지 않았습니다. 이 미리보기 단계는 MCP를 통한 실제 모델 로드·화면 전환까지 수행한 결과입니다.
