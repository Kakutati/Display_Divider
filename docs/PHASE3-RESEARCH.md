# Phase 3 조사: 가상 디스플레이 기반 존 가두기

조사일: 2026-07-31. 목표는 DESIGN.md Phase 3 —
**존 해상도의 가상 모니터를 만들고, 게임을 거기서 전체화면 실행시킨 뒤, 그 화면을 물리 모니터의 존 위치에 합성**해서
독점 전체화면·Chromium F11처럼 창 관리로는 불가능한 케이스를 해결하는 것.

## 1. 오픈소스 IddCx 드라이버 후보 비교

| 항목 | parsec-vdd | Virtual-Display-Driver (VirtualDrivers/MikeTheTech) | SudoVDA (SudoMaker) | virtual-display-rs |
|------|-----------|------------------------------------------------------|---------------------|--------------------|
| 서명 | ✅ 서명됨 (Parsec 드라이버 + SignPath) | ✅ SignPath 서명 릴리스 | 릴리스 서명 명시 없음 (Apollo 번들로 배포됨) | ❌ 자가 서명 인증서 필요 |
| 라이선스 | MIT (래퍼) — 단, 드라이버 바이너리는 Parsec 소유물 | MIT | MIT / CC0 | (Rust, RC 단계) |
| 동적 생성 API | ✅ IOCTL C API (헤더 1개, 최대 16개, keep-alive 핑 필요) | ⚠️ XML 설정 + GUI(VDC) 중심 — 프로그램적 제어 약함 | ✅ Apollo가 클라이언트별 가상 모니터를 동적 생성 (IOCTL, Apollo 소스 참조 필요) | API 있음 |
| 임의 해상도 | ⚠️ 레지스트리 프리셋 5개 제한 | 커스텀 해상도/소수점 주사율 (사전 정의) | ✅ 임의 해상도/주사율 (Apollo가 클라이언트 해상도 자동 매칭) | 지원 |
| HDR | ❌ (EDID 고정) | ✅ 10/12bit (Win11 23H2+) | ✅ (Win11 24H2) | ❌ 8bit SDR |
| HW 커서 | ❌ | ✅ | (MTT 계열) | - |
| 유지보수 | 활발 | 활발 (425+ 커밋) | 활발 (Apollo 생태계가 실사용) | RC 정체 |

**평가**
- **SudoVDA**: 우리 요구(존 크기 = 임의 해상도, 프로그램에서 동적 생성/제거)에 가장 근접. Apollo가 프로덕션에서
  똑같은 패턴(클라이언트 접속 → 해상도 맞는 가상 모니터 생성)으로 사용 중이라 실증됨. IOCTL 사용법은 Apollo 소스에서 확인 필요.
- **parsec-vdd**: API가 가장 깔끔하고 문서화됨. 단점은 임의 해상도가 아니라 프리셋(레지스트리 5개) — 존 크기를
  프리셋으로 등록하는 우회가 필요하고, 존 크기를 바꿀 때마다 레지스트리 갱신 + 재연결. Parsec 드라이버 재배포의 법적 회색지대 유의.
- **Virtual-Display-Driver**: 드라이버 품질/서명/기능(HDR, EDID)은 최상급이지만 "앱이 수시로 만들고 지우는" 용도보다
  "고정 가상 모니터" 용도에 맞춰져 있음.
- **virtual-display-rs**: 자가 서명(테스트 서명 모드) 요구 → 일반 사용자 배포에 부적합. 제외.

## 2. 화면 합성(뷰어) 경로

| 항목 | Windows.Graphics.Capture (WGC) | DXGI Desktop Duplication (DDA) |
|------|-------------------------------|--------------------------------|
| 지연/프레임 | 일부 시나리오 60fps 제한 보고 (Apollo #676) | 더 낮은 지연, 120fps+ 가능 |
| GPU 제약 | 크로스 GPU 동작 | 캡처와 표시가 같은 GPU여야 유리 |
| 난이도 | WinRT API, 비교적 간단 | D3D11 직접 제어 |
| 커서 | 캡처 옵션으로 포함 가능 | 별도 그리기 필요 |

**평가**: MVP는 WGC로 시작(간단·안전), 60Hz 존이면 충분. 지연이 문제로 확인되면 DDA로 교체. 렌더는 뷰어 창(D3D11/DirectComposition)에 프레임 그대로 출력.

## 3. 핵심 리스크 (난이도 순)

1. **입력 전달이 가장 큰 작업** — 게임 창은 가상 모니터에 있으므로, 물리 모니터의 존(뷰어 창)을 클릭해도 게임에 닿지 않는다.
   뷰어가 마우스 좌표를 가상 모니터 좌표로 매핑해 `SendInput`으로 전달해야 하고, FPS류의 상대 마우스 입력(RawInput),
   커서 클립과의 상호작용도 처리해야 한다. 사실상 "로컬 Moonlight 클라이언트"를 만드는 셈. 키보드/패드는 포커스만 게임에 있으면 되므로 상대적으로 쉬움.
   → PoC에서 가장 먼저 검증할 것. 1차 릴리스는 "컨트롤러/키보드 게임 우선 지원"으로 범위를 줄이는 것도 현실적.
2. **임의 해상도 대응** — 존 크기는 임의 픽셀. SudoVDA는 동적 지원, parsec-vdd는 프리셋 우회 필요.
3. **지연** — 캡처→렌더로 1프레임 내외 추가. 경쟁 FPS에는 부적합, 일반 게임/앱에는 수용 가능.
4. **커서 표시** — WGC 캡처 옵션 또는 뷰어에서 직접 그리기.
5. **드라이버 설치 UX** — 관리자 권한 설치 필요. 서명된 릴리스(parsec-vdd, VDD)는 설치 마찰이 적음.

## 4. 권장 진행안

1. **PoC-A (드라이버)**: SudoVDA로 존 해상도 가상 모니터 동적 생성/제거 실증. Apollo 소스에서 IOCTL 프로토콜 확인.
   실패/난항 시 parsec-vdd + 프리셋 우회로 폴백.
2. **PoC-B (뷰어)**: WGC로 가상 모니터 캡처 → 존 위치의 테두리 없는 창에 렌더. 체감 지연 측정 (60Hz 기준 목표 ≤ 2프레임).
3. **PoC-C (입력)**: 뷰어 → 가상 모니터 입력 포워딩 (절대 좌표 클릭 → 상대 입력 게임 순).
4. PoC 통과 시 `DisplayDivider.VirtualDisplay` 모듈로 통합 — 존에 "가상 존" 속성을 주고,
   규칙에 "가상 모니터에서 실행" 모드를 추가.

## 5. 출처

- https://github.com/nomi-san/parsec-vdd (+ docs/PARSEC_VDD_SPECS.md)
- https://github.com/VirtualDrivers/Virtual-Display-Driver
- https://github.com/SudoMaker/SudoVDA
- https://github.com/ClassicOldSong/Apollo (SudoVDA 실사용, WGC 60fps 이슈 #676)
- https://github.com/MolotovCherry/virtual-display-rs
- https://github.com/roshkins/IddSampleDriver, https://github.com/ge9/IddSampleDriver
- https://obsproject.com/forum/threads/windows-graphics-capture-vs-dxgi-desktop-duplication.149320/
- https://learn.microsoft.com/en-us/windows-hardware/drivers/display/indirect-display-driver-model-overview
