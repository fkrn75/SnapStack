# SnapStack 진행 현황 · 핸드오프

> 작업 재개용 문서. 마지막 업데이트: **2026-06-16**

## 현재 상태: 🟢 MVP 핵심 동작 (필수 4대 기능 중 3개 관통)

캡쳐 → 순서대로 히스토리 → 미리보기 → 클립보드 복사/저장까지 **실제 동작**(빌드·기동·UI 검증 완료). 남은 핵심은 **편집/페인팅(#2)**.

---

## ✅ 완료

| 영역 | 산출물 | 커밋 |
|------|--------|------|
| 문서 | 개발계획서, 상세 기능명세서 | `499f819` |
| M0 뼈대 | .NET8 WPF · DI · PerMonitorV2 매니페스트 · 폴더구조 | `b1ad618` |
| 공유 계약 | `CaptureItem`·`AppSettings` + 6개 서비스 인터페이스 | `604c704` |
| MVP 핵심 | 캡쳐·히스토리·클립보드·저장·설정 + UI 배선 | `317450a` |

### 동작하는 기능
- **캡쳐**: 영역(오버레이 드래그)·전체화면·전체 가상화면·직전 영역·지연(3초)
- **히스토리(#1)**: 캡쳐 순서대로 누적(`Sequence` SSOT)·썸네일 스트립·선택·삭제·전체 비우기
- **미리보기**: 선택 항목 `EffectiveImage` 표시
- **클립보드 복사(#3)** + **외부앱 붙여넣기(#4)**: PNG + DIB 다중 포맷
- **저장**: 빠른 저장(PNG/JPG/BMP, 파일명 규칙)

---

## ⬜ 미구현 (다음 작업, 우선순위 순)

### 1. 편집기 / 페인팅 (#2, EDT) ← **최우선 (필수 4대 마지막)**
- `Views/EditorWindow.xaml(.cs)` + `ViewModels/EditorViewModel.cs` + `Editor/*` 신규
- 도구: 펜·형광펜(`InkCanvas`), 도형/직선/화살표/텍스트/번호(`Canvas`+`Adorner`), 모자이크/블러(`WriteableBitmap`), 자르기, 색/굵기, Undo/Redo(Command 스택)
- **진입점(이미 준비됨)**:
  - `IHistoryService.MarkEdited(item, composite)` — 편집 완료 시 호출하면 EditedComposite 채우고 썸네일 갱신
  - `CaptureItem.EditedComposite` / `IsEdited` / `EffectiveImage` 규칙 — 편집본이 자동으로 복사·저장·미리보기에 반영됨
  - `EditorViewModel.FlattenToBitmap()`로 평탄화(Pbgra32 frozen) → `MarkEdited` (§4.0 평탄화 계약)
  - MainWindow 히스토리 더블클릭 → `EditorWindow` 열기 (HIS-03) 배선 필요
- 명세: `docs/FUNCTIONAL_SPEC.md` §3

### 2. 시스템 통합 (§5)
- `Services/HotkeyService.cs` (인터페이스 있음) — Win32 `RegisterHotKey` + `HwndSource.AddHook`, 메인 윈도우 핸들 필요
- 시스템 트레이 — `H.NotifyIcon.Wpf` 패키지 추가 후 `TaskbarIcon`
- `Views/SettingsWindow` — 설정 화면(`SettingsService` 인터페이스·구현 있음)

### 3. 기타
- 클립보드 CF_DIBV5(투명) 추가 (현재 PNG+DIB만, v1 불투명 전제라 충분)
- 창 자동 인식 캡쳐(CAP-02, 현재 영역 선택으로 대체)
- 세션 영속화(HIS-05), 스크롤 캡쳐, 화면 녹화 등 (v2 로드맵)

---

## 🔧 빌드 · 실행

> ⚠️ .NET SDK(8.0.422)는 설치돼 있으나 PATH 미갱신 — 새 PowerShell마다 PATH 주입 필요.

```powershell
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

# 빌드
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
├─ App.xaml(.cs)          # DI 컨테이너 구성(ConfigureServices에 서비스 등록)
├─ MainWindow.xaml(.cs)   # 메인 화면(도구바·히스토리 스트립·미리보기)
├─ app.manifest          # PerMonitorV2 DPI
├─ Models/
│  ├─ CaptureItem.cs      # §4.0 스키마·EffectiveImage 규칙·CaptureMode
│  └─ AppSettings.cs      # 설정·단축키 기본값·ImageFormatKind
├─ Services/
│  ├─ I*.cs               # 6개 인터페이스(계약)
│  ├─ HistoryService.cs   # 순서 SSOT·썸네일·Dispatcher 마샬링
│  ├─ ClipboardService.cs # PNG+DIB 다중 포맷
│  ├─ FileExportService.cs# PNG/JPG/BMP 저장
│  ├─ SettingsService.cs  # JSON 영속
│  └─ CaptureService.cs   # BitBlt + 오버레이
├─ Views/CaptureOverlay.xaml(.cs)  # 영역 선택 오버레이(DIP→물리 변환)
├─ ViewModels/MainViewModel.cs     # 캡쳐 명령·히스토리·복사/저장
└─ Interop/
   ├─ ScreenMetrics.cs    # 가상화면 물리 좌표(GetSystemMetrics)
   └─ NativeMethods.cs    # GDI DeleteObject
```

---

## 📝 구현 방식 메모
- **코드 구현은 지휘자 단일 writer 순차** — 여러 에이전트 동시 `.cs` 편집은 `csproj`/DI 등록 충돌로 빌드를 깨뜨림. (명세·조사 단계는 팀 오케스트라 병렬로 진행 후 통합했음.)
- 패키지: `CommunityToolkit.Mvvm`(MVVM 소스 생성기) · `Microsoft.Extensions.DependencyInjection` · `System.Drawing.Common`(BitBlt).
- 모든 공유 `BitmapSource`는 `Freeze()` — 교차 스레드 안전.
