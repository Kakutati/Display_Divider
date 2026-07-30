# Display_Divider

DisplayFusion의 "모니터 분할" 기능만 담은 Windows용 창 분할 유틸리티.
설계 문서: [docs/DESIGN.md](docs/DESIGN.md)

## Phase 1 기능 (현재)

- **존 레이아웃**: 모니터별로 프리셋(2분할, 3열, 상/하, 2×2, 70:30) 또는 임의 비율(`70,30` 등)로 분할. 비율로 저장되므로 해상도가 바뀌어도 유지됨
- **드래그 스냅**: 창을 드래그하면 존 오버레이 표시, **Ctrl을 누른 채 드롭**하면 존에 맞춰짐 (트레이 메뉴에서 "항상 스냅" 모드로 변경 가능, 이때는 Shift로 예외)
- **가짜 최대화**: 최대화 버튼을 누르면 존 크기까지만 확장. **Shift+최대화**는 모니터 전체. 존-최대화 상태에서 다시 최대화하면 원래 크기로 토글
- **전역 단축키**: `Ctrl+Alt+←/→` 이전/다음 존으로 이동, `Ctrl+Alt+↑` 현재 존 채우기
- 트레이 상주. 설정 파일: `%APPDATA%\DisplayDivider\layout.json`

## 빌드 / 실행

```powershell
dotnet build
dotnet test
dotnet run --project src/DisplayDivider.App
```

처음 실행하면 존 편집기가 자동으로 열립니다. 모니터를 선택하고 프리셋을 누른 뒤 **저장**하세요.

## 프로젝트 구조

| 프로젝트 | 역할 |
|----------|------|
| `DisplayDivider.Core` | 존 계산·레이아웃 저장 (순수 로직, 단위 테스트 대상) |
| `DisplayDivider.Win32` | Win32 연동 — 모니터 열거, 창 이벤트 훅, 창 이동, DWM 프레임 보정 |
| `DisplayDivider.App` | WPF 트레이 앱 — 존 편집기, 드래그 오버레이, 컨트롤러, 단축키 |
| `DisplayDivider.Core.Tests` | xUnit 테스트 |

## 알려진 제한 (Phase 1)

- 관리자 권한으로 실행된 창은 일반 권한에서 이동 불가 (앱을 관리자로 실행하면 해결)
- 테두리 없는 전체화면 게임 가두기는 Phase 2, 독점 전체화면은 Phase 3 (가상 디스플레이) 범위
