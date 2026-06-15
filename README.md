# SnapStack 📸

> **찍고(Snap) → 쌓고(Stack) → 그리고 → 붙여넣는다.**
> 알캡쳐(ALCapture) 류의 Windows 화면 캡쳐 · 주석 · 클립보드 도구 (개인용 / 포트폴리오)

![status](https://img.shields.io/badge/status-planning-orange)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![stack](https://img.shields.io/badge/.NET%208-WPF-512BD4)
![license](https://img.shields.io/badge/license-MIT-green)

---

## 🚧 현재 상태

**계획 단계** — 아직 코드는 없으며, 설계 문서를 먼저 작성 중입니다.

- 📄 [개발계획서 (DEVELOPMENT_PLAN.md)](docs/DEVELOPMENT_PLAN.md) ✅
- 📄 기능명세서 (FUNCTIONAL_SPEC.md) — 예정

## ✨ 핵심 기능 (목표)

| 기능 | 설명 |
|------|------|
| 🖼 **캡쳐 히스토리** | 캡쳐한 이미지를 **순서대로** 썸네일 스트립에 누적, 언제든 다시 선택 |
| 🎨 **페인팅 / 주석** | 펜·형광펜·도형·화살표·텍스트·번호·모자이크/블러·자르기 + Undo/Redo |
| 📋 **클립보드 복사** | 편집한 이미지를 클립보드에 저장 (PNG+DIB 다중 포맷으로 호환성 확보) |
| 📌 **외부 앱 붙여넣기** | 한글·Word·카카오톡 등 다른 프로그램에 Ctrl+V로 곧바로 붙여넣기 |
| ✂️ 다양한 캡쳐 | 영역 지정 · 활성 창 · 전체 화면 · 직전 영역 재캡쳐 · 지연 캡쳐 |
| ⚙️ 시스템 통합 | 전역 단축키 · 시스템 트레이 상주 · 설정 |

## 🛠 기술 스택

- **언어/런타임**: C# 12, .NET 8 (LTS)
- **UI**: WPF + MVVM (CommunityToolkit.Mvvm)
- **플랫폼**: Windows 10 / 11 (x64) 전용
- **주요 기술**: Win32 Interop(BitBlt·RegisterHotKey·Clipboard), WPF InkCanvas, Per-Monitor DPI

## 🗺 로드맵

```
M0 셋업 → M1 캡쳐코어 → M2 히스토리(순서대로) → M3 편집/페인팅 → M4 클립보드/붙여넣기 → M5 시스템통합 → M6 배포
```

자세한 내용은 [개발계획서](docs/DEVELOPMENT_PLAN.md)를 참고하세요.

## ⚡ 개발 환경 (착수 전 필요)

```powershell
# .NET 8 SDK 설치 후
dotnet --version
dotnet new wpf -n SnapStack -o src/SnapStack
```

## 📄 라이선스

[MIT](LICENSE) © 2026 fkrn75
