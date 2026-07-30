# Display_Divider 설계 문서

Windows용 모니터 분할 유틸리티. DisplayFusion의 "Monitor Splitting" 기능만 독립 구현하되,
전체화면 프로그램(게임 포함)도 분할 영역(존) 크기만큼만 차지하는 **가상 모니터 방식**을 목표로 한다.

## 1. 요구사항

| 구분 | 내용 |
|------|------|
| 필수 | 물리 모니터를 N개의 존으로 분할 (가로/세로/그리드, 비율 지정) |
| 필수 | 일반 창의 최대화 시 존 크기까지만 확장 |
| 필수 | 창 드래그/단축키로 존 간 이동 |
| 필수 | 레이아웃 저장/불러오기, 트레이 상주 |
| 목표 | 전체화면(게임 포함) 앱을 존 안에 가두기 |
| 제외 | 배경화면/화면보호기/작업표시줄 분할, 그 외 DisplayFusion 기능 전부 |

## 2. 기술적 실현 가능성 — 창의 3가지 유형

전체화면을 존에 가두는 문제는 창 유형에 따라 난이도가 완전히 다르다.
**DisplayFusion Pro조차 독점 전체화면은 존에 가두지 못한다**는 점이 설계의 출발점이다.

### Tier A. 일반 창 (탐색기, 브라우저, IDE 등)
- 일반적인 창 관리 API로 완전히 제어 가능.
- 최대화 가두기: 창이 최대화되는 순간을 감지해 "존 크기의 일반 창"으로 되돌리는
  **가짜 최대화(fake maximize)** 기법 사용. DisplayFusion도 같은 방식.
- 난이도: 낮음~중간.

### Tier B. 테두리 없는 전체화면 (borderless windowed) — 최신 게임 대부분
- 실체는 "모니터 해상도 크기의 일반 팝업 창"이므로 `SetWindowPos`로 존 크기로 강제 리사이즈 가능.
- 일부 게임은 주기적으로 모니터 해상도를 다시 조회해 창을 되돌리므로,
  감시 루프로 재적용하거나 게임별 예외 규칙이 필요.
- 게임 내부 렌더 해상도는 게임이 결정하므로, 리사이즈 시 찌그러져 보일 수 있음
  (게임 설정에서 존 해상도로 맞추면 해결).
- 난이도: 중간.

### Tier C. 독점 전체화면 (exclusive fullscreen)
- GPU가 디스플레이 출력 자체를 게임에 넘기는 방식이라 **창 관리 기법으로는 불가능**.
- 유일한 실제 해법: **가상 디스플레이 드라이버(IddCx)** 경로.
  1. IddCx(Indirect Display Driver) 기반 가상 모니터를 존 해상도로 생성
     → Windows와 게임이 "진짜 모니터"로 인식.
  2. 게임은 가상 모니터에서 전체화면 실행.
  3. 뷰어(compositor) 프로세스가 `Windows.Graphics.Capture`로 가상 모니터 화면을 캡처해
     물리 모니터의 해당 존 위치에 렌더링.
- 비용: 커널/UMDF 드라이버 개발(C++), 드라이버 서명(배포 시 EV 인증서 + MS 어테스테이션 서명),
  캡처→렌더 1프레임 내외의 지연.
- 대안: 검증된 오픈소스 IddCx 드라이버(예: Virtual Display Driver, virtual-display-rs 계열)를
  구성요소로 활용하고 뷰어만 직접 개발 — 초기 개발량을 크게 줄일 수 있음.
- 난이도: 높음. **별도 페이즈로 분리.**

### 안티치트 주의사항
게임 프로세스에 DLL 인젝션/후킹을 하면 안티치트(EAC, BattlEye 등)에 의해 차단·제재될 수 있다.
따라서 **게임 프로세스에는 절대 코드를 주입하지 않는다.**
Tier B는 외부에서 `SetWindowPos`만 사용(안전), Tier C는 드라이버+캡처 방식(주입 없음)으로 해결한다.

## 3. 기술 스택 (유지보수 우선)

| 구성요소 | 선택 | 근거 |
|----------|------|------|
| 본체 앱 (트레이, 존 엔진, UI) | **C# / .NET 8 LTS + WPF** | Windows 유틸리티 표준 스택. P/Invoke로 Win32 전부 접근 가능. PowerToys FancyZones도 유사 구조라 참고 자료 풍부 |
| Win32 상호운용 | CsWin32 (Microsoft 소스 생성기) | 수기 P/Invoke 선언 제거 → 유지보수성↑ |
| 훅 DLL (선택, Phase 2) | C++ (x64/x86 각 1개) | `WM_GETMINMAXINFO` 가로채기가 필요할 때만. 일반 앱 한정 |
| 가상 디스플레이 (Phase 3) | 오픈소스 IddCx 드라이버 + C# 뷰어(D3D/Composition) | 드라이버 자체 개발 회피 |
| 설정 저장 | JSON (`%APPDATA%\DisplayDivider\`) | 단순, diff 가능 |
| 테스트 | xUnit (존 계산 로직은 순수 함수로 분리) | UI 없이 핵심 로직 검증 |

## 4. 아키텍처

```
┌─────────────────────────────────────────────┐
│ DisplayDivider.App (WPF, 트레이 상주)        │
│  ├─ TrayIcon / 설정 창                       │
│  ├─ ZoneEditor    : 존 레이아웃 편집 오버레이 │
│  └─ SnapOverlay   : 드래그 시 존 하이라이트   │
├─────────────────────────────────────────────┤
│ DisplayDivider.Core (순수 로직, UI 무관)     │
│  ├─ LayoutEngine  : 존 계산 (분할/비율/그리드)│
│  ├─ LayoutStore   : JSON 저장/불러오기       │
│  └─ Rules         : 앱별 예외 규칙           │
├─────────────────────────────────────────────┤
│ DisplayDivider.Win32 (OS 연동 계층)          │
│  ├─ MonitorService   : EnumDisplayMonitors,  │
│  │                     DPI, 핫플러그 감지     │
│  ├─ WindowTracker    : SetWinEventHook       │
│  │   (창 생성/이동/최대화/전체화면 감지)      │
│  ├─ WindowMover      : SetWindowPos,         │
│  │                     DwmGetWindowAttribute │
│  └─ HotkeyService    : RegisterHotKey        │
├─────────────────────────────────────────────┤
│ [Phase 3] DisplayDivider.VirtualDisplay      │
│  ├─ IddCx 드라이버 (오픈소스 활용)            │
│  └─ ZoneViewer : Windows.Graphics.Capture    │
│                  → 존 위치에 프레임 렌더링    │
└─────────────────────────────────────────────┘
```

### 핵심 동작 흐름

**최대화 가두기 (Phase 1, 훅 DLL 없이):**
1. `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE / EVENT_SYSTEM_MOVESIZEEND)`으로 전역 감시.
2. 창이 `WS_MAXIMIZE` 상태가 되면 → 창이 속한 존 판정(창 중심점 기준)
   → `ShowWindow(SW_RESTORE)` 후 `SetWindowPos`로 존 rect에 맞춤.
3. 창 핸들별로 "존-최대화 상태"를 기억해, 다시 최대화 버튼을 누르면 원래 크기로 토글.
4. Shift+최대화 시 가두기 무시(모니터 전체 최대화) — DisplayFusion과 동일한 UX.

**드래그 스냅:**
1. `EVENT_SYSTEM_MOVESIZESTART` → 오버레이 표시.
2. 드래그 중 커서 위치의 존을 하이라이트.
3. `EVENT_SYSTEM_MOVESIZEEND` → 해당 존 rect로 스냅.
   (DWM 확장 프레임 보정: `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`로
   그림자 여백을 빼고 배치해야 픽셀이 정확히 맞음)

**전체화면 감지 (Tier B):**
- 창 rect == 모니터 rect && 스타일에 캡션 없음 → borderless 전체화면으로 판정
- 규칙(Rules)에서 해당 앱이 "존 가두기" 대상이면 존 rect로 리사이즈, 재발 시 재적용.

### 주의해야 할 Windows 특성
- **DPI**: 모니터별 DPI가 다르면 물리/논리 좌표가 어긋난다. 앱 매니페스트에
  `PerMonitorV2` 선언 필수. 존 계산은 물리 픽셀 기준으로 통일.
- **권한**: 관리자 권한으로 실행된 창은 일반 권한 앱이 이동시킬 수 없음
  → `uiAccess` 또는 "관리자로 실행" 옵션 제공.
- **최소화 애니메이션/스냅 어시스트 충돌**: Windows 기본 Snap(Win+화살표, Snap Layouts)과
  기능이 겹치므로 설정에서 상호 배타 옵션 제공.

## 5. 로드맵

| 페이즈 | 범위 | 결과물 |
|--------|------|--------|
| **1. MVP** | 존 편집기, 레이아웃 저장, 드래그 스냅, 단축키 스냅, 가짜 최대화(Tier A) | 일반 창에 대해 DisplayFusion 분할과 동등 |
| **2. 게임(창모드)** | borderless 전체화면 감지·가두기, 앱별 규칙, 훅 DLL(선택) | 최신 게임 대부분 커버 |
| **3. 가상 모니터** | IddCx 가상 디스플레이 + 존 뷰어 합성 | 독점 전체화면까지 존에 표시 (DisplayFusion도 못 하는 영역) |

Phase 1–2는 C# 단독으로 완결되며, Phase 3은 독립 모듈이라 실패해도 1–2 결과물에 영향 없음.

## 6. 리스크

| 리스크 | 영향 | 대응 |
|--------|------|------|
| 독점 전체화면 가두기 실패 | 핵심 차별점 상실 | Phase 3 분리, borderless 강제 전환 옵션을 차선책으로 제공 |
| 드라이버 서명 비용 (배포 시) | 개인 배포 어려움 | 개발 중 테스트 서명 모드, 배포는 기존 서명된 오픈소스 드라이버 활용 |
| 게임이 창 리사이즈에 저항 | 일부 게임 미지원 | 앱별 규칙(재적용 주기, 제외 목록) |
| 안티치트 오탐 | 계정 제재 위험 | 게임 프로세스 주입 금지 원칙 유지 |
