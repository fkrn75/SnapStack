# SnapStack 진행 현황 · 핸드오프

> 작업 재개용 문서. 마지막 업데이트: **2026-06-16**

## 현재 상태: 🟢 필수 4대 기능 + 시스템 통합 + v2(단축키 재지정 UI·트레이 상주) 완료

캡쳐 → 순서 히스토리 → 미리보기 → **편집/페인팅** → 클립보드 복사 → 외부앱 붙여넣기 / 저장까지 **실제 동작**. 빌드 0에러/0경고 · 기동 · **캡쳐→자동선택→편집기(11개 도구 렌더)·설정창·트레이/전역단축키 기동을 런타임 검증 완료**. v2 추가: **단축키 재지정 UI·트레이 상주(close-to-tray)** 통합·빌드 0/0·런타임 시각검증 완료(2026-06-16, 미커밋).

---

## ✅ 완료

| 영역 | 산출물 | 커밋 |
|------|--------|------|
| 문서 | 개발계획서, 상세 기능명세서 | `499f819` |
| M0 뼈대 | .NET8 WPF · DI · PerMonitorV2 매니페스트 · 폴더구조 | `b1ad618` |
| 공유 계약 | `CaptureItem`·`AppSettings` + 서비스 인터페이스 | `604c704` |
| MVP 핵심 | 캡쳐·히스토리·클립보드·저장·설정 + UI 배선 | `317450a` |
| **편집기 + 시스템 통합** | 페인팅·전역단축키·트레이·설정창 | `c89caa2` |
| **v2 단축키 재지정 + 트레이 상주** | `HotkeyCaptureBox`·`HotkeyService.Reapply`·close-to-tray | (미커밋) |

### 동작하는 기능 — 필수 4대 전부 ✓
- **캡쳐**: 영역(오버레이 드래그)·**창(창 선택 오버레이 → PrintWindow, 가려져도 캡쳐)**·전체화면·전체 가상화면·직전 영역·지연(3초)
- **히스토리(#1)**: 캡쳐 순서대로 누적(`Sequence` SSOT)·**새 캡쳐 자동 선택**·썸네일 스트립·삭제·전체 비우기
- **편집/페인팅(#2)**: `EditorWindow` — 펜·형광펜·사각형·타원·직선·화살표·텍스트·번호·모자이크·자르기·선택 **11개 도구** + 색/굵기 + Undo/Redo. 적용 시 `FlattenToBitmap`(원본 픽셀 1:1, Pbgra32 frozen) → `IHistoryService.MarkEdited`로 편집본이 복사·저장·미리보기에 자동 반영. 진입: 히스토리 더블클릭 / 편집 버튼
- **클립보드 복사(#3)** + **외부앱 붙여넣기(#4)**: PNG + DIB 다중 포맷 (편집기에서도 복사 가능)
- **저장**: 빠른 저장(PNG/JPG/BMP, 파일명 규칙)
- **시스템 통합(§5)**: 전역 단축키(`HotkeyService`=Win32 RegisterHotKey, 설정 기반·선점된 키 skip)·시스템 트레이(`TrayService`=WinForms NotifyIcon, 텍스트 "S" 아이콘·메뉴·더블클릭)·설정창(`SettingsWindow`=AppSettings 편집·영속화)
- **v2 단축키 재지정(SYS-04)**: 설정창 '단축키' 탭에서 `HotkeyCaptureBox`(TextBox 파생, 키 조합 캡쳐 → "Ctrl+Shift+W" 문자열, 순수 modifier 무시·Esc/Back/Del 비우기)로 재지정. 저장 시 중복 제스처 검증(원자적) 후 `IHotkeyService.Reapply`로 전역 핫키 즉시 재등록(실패 키 알림). 저장형식은 기존 `Hotkeys`(Dictionary<string,string>) 무수정 호환
- **v2 트레이 상주(close-to-tray)**: 메인창 X/최소화 시 종료 대신 트레이로 숨김(`MinimizeToTrayOnClose` 기본 ON·`MinimizeToTrayOnMinimize` 기본 OFF, '트레이' 탭). `App.ShutdownMode=OnExplicitShutdown`로 창 닫혀도 앱 유지, 트레이 '종료'에서만 실제 종료(`_reallyExit`→`Shutdown`)·종료 시 DI 컨테이너 Dispose로 트레이/핫키 정리. 첫 숨김 시 BalloonTip 안내

---

## ⬜ 미구현 (v2 로드맵)
- 클립보드 CF_DIBV5(투명) 추가 (현재 PNG+DIB만, v1 불투명 전제라 충분)
- 세션 영속화(HIS-05), 스크롤 캡쳐, 화면 녹화
- ~~단축키 **재지정** UI · 트레이 **최소화-상주**~~ → **v2 완료(2026-06-16)** — 위 '동작하는 기능' v2 항목 참조
- 편집기 드로잉→적용 round-trip은 코드/계약상 검증 완료(`Apply`→`MarkEdited`), 사용자 실사용 1회 확인 권장

---

## 🔧 빌드 · 실행

> ⚠️ .NET SDK(8.0.422)는 설치돼 있으나 PATH 미갱신 — 새 PowerShell마다 PATH 주입 필요.

```powershell
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

# 빌드 (현재 0에러/0경고)
dotnet build src\SnapStack\SnapStack.csproj -c Debug

# 실행
dotnet run --project src\SnapStack\SnapStack.csproj
# 또는 빌드 산출물 직접 실행
src\SnapStack\bin\Debug\net8.0-windows\SnapStack.exe
```

---

## 📁 프로젝트 구조

```
src/SnapStack/
├─ App.xaml(.cs)          # DI 컨테이너(서비스·VM·윈도우 등록)
├─ MainWindow.xaml(.cs)   # 메인(도구바·히스토리·미리보기) + 단축키/트레이/편집기/설정 배선
├─ app.manifest          # PerMonitorV2 DPI
├─ Models/
│  ├─ CaptureItem.cs      # §4.0 스키마·EffectiveImage 규칙·CaptureMode
│  └─ AppSettings.cs      # 설정·단축키 기본값·ImageFormatKind
├─ Editor/                # 편집기 도구 모델
│  ├─ EditorTool.cs       # 11개 도구 enum
│  ├─ UndoStack.cs        # Undo/Redo 스택
│  ├─ MosaicHelper.cs     # 모자이크 픽셀화
│  └─ ArrowGeometry.cs    # 화살표 도형
├─ Services/
│  ├─ I*.cs               # 인터페이스(계약)
│  ├─ HistoryService.cs   # 순서 SSOT·자동선택·썸네일·Dispatcher 마샬링
│  ├─ ClipboardService.cs # PNG+DIB 다중 포맷
│  ├─ FileExportService.cs# PNG/JPG/BMP 저장
│  ├─ SettingsService.cs  # JSON 영속
│  ├─ CaptureService.cs   # BitBlt + 오버레이
│  ├─ HotkeyService.cs    # 전역 단축키(RegisterHotKey + HwndSource 훅)
│  └─ TrayService.cs      # 트레이(WinForms NotifyIcon, 텍스트 아이콘)
├─ Views/
│  ├─ CaptureOverlay.xaml(.cs)  # 영역 선택 오버레이(DIP→물리 변환)
│  ├─ EditorWindow.xaml(.cs)    # 편집기(캔버스·툴바·평탄화)
│  └─ SettingsWindow.xaml(.cs)  # 설정창(탭 UI)
├─ ViewModels/
│  ├─ MainViewModel.cs    # 캡쳐 명령·히스토리·복사/저장·RefreshSelectedPreview
│  ├─ EditorViewModel.cs  # 도구상태·적용/복사/저장·Undo
│  └─ SettingsViewModel.cs
└─ Interop/
   ├─ ScreenMetrics.cs    # 가상화면 물리 좌표(GetSystemMetrics)
   └─ NativeMethods.cs    # GDI DeleteObject
```

---

## 📝 구현 방식 메모
- **코드 통합은 지휘자 단일 writer 순차** — 팀 오케스트라로 팀원(editor·sysint)이 **서로 겹치지 않는 신규 파일**을 병렬 작성하되, 공유 파일(`csproj`·`App.xaml.cs`·`MainWindow`·`MainViewModel`)의 통합·빌드는 지휘자가 단독으로. (동시 .cs 편집은 csproj/DI 충돌로 빌드를 깨뜨림.)
- **트레이는 WinForms NotifyIcon** — `H.NotifyIcon.Wpf`(2.4.x)는 `System.Drawing.Common ≥10.0.0`(.NET10용)을 요구해 .NET8의 8.0.0과 NU1605(다운그레이드) 충돌. 내장 `System.Windows.Forms.NotifyIcon`으로 대체(외부 의존성 0). 단 `UseWindowsForms`가 전역 using(`System.Windows.Forms`·`System.Drawing`)을 주입해 WPF 타입과 CS0104 충돌 → csproj `<Using Remove>`로 두 전역 using 제거, `WFAC010`(매니페스트 DPI) 경고는 `NoWarn`.
- 패키지: `CommunityToolkit.Mvvm`(MVVM 소스 생성기) · `Microsoft.Extensions.DependencyInjection` · `System.Drawing.Common`(BitBlt·트레이 아이콘).
- 모든 공유 `BitmapSource`는 `Freeze()` — 교차 스레드 안전.
```
