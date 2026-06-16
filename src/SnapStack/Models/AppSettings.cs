using System.IO;

namespace SnapStack.Models;

/// <summary>저장 포맷(§4.4 SAV-02).</summary>
public enum ImageFormatKind { Png, Jpg, Bmp }

/// <summary>
/// 사용자 설정(§5). JSON으로 %APPDATA%\SnapStack\settings.json 에 영속.
/// 기본값은 명세 §2.3·§4.4·§5 기준.
/// </summary>
public sealed class AppSettings
{
    /// <summary>스키마 버전(마이그레이션용).</summary>
    public int SchemaVersion { get; set; } = 1;

    // ── 저장(§4.4) ──
    /// <summary>기본 저장 폴더. 기본 %USERPROFILE%\Pictures\SnapStack.</summary>
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapStack");

    /// <summary>파일명 템플릿. SAV-03.</summary>
    public string FileNameTemplate { get; set; } = "SnapStack_{yyyyMMdd}_{HHmmss}_{seq:000}";

    /// <summary>빠른 저장 기본 포맷.</summary>
    public ImageFormatKind DefaultFormat { get; set; } = ImageFormatKind.Png;

    /// <summary>자동 저장(SAV-04, 기본 OFF).</summary>
    public bool AutoSave { get; set; }

    // ── 캡쳐(§2) ──
    /// <summary>지연 캡쳐 시간(초). CAP-05.</summary>
    public int DelaySeconds { get; set; } = 3;

    /// <summary>캡쳐 후 동작: 편집기 자동 열기.</summary>
    public bool OpenEditorAfterCapture { get; set; } = true;

    // ── 시스템(§5) ──
    /// <summary>Windows 시작 시 자동 실행. SYS-03.</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>히스토리 세션 영속화. HIS-05.</summary>
    public bool PersistHistory { get; set; } = true;

    /// <summary>히스토리 보존 개수 상한.</summary>
    public int HistoryLimit { get; set; } = 50;

    /// <summary>전역 단축키 매핑(동작명 → 제스처 문자열). 기본은 §5.1 마스터표.</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = new()
    {
        ["CaptureRegion"]      = "Ctrl+Shift+A",
        ["CaptureWindow"]      = "Ctrl+Shift+W",
        ["CaptureFullScreen"]  = "Ctrl+Shift+F",
        ["CaptureVirtualFull"] = "Ctrl+Shift+G",
        ["CaptureLastRegion"]  = "Ctrl+Shift+R",
        ["CaptureDelayed"]     = "Ctrl+Shift+D",
    };

    // ── 트레이(§5 SYS-02) ──
    /// <summary>메인창을 닫으면 종료하지 않고 트레이로 최소화(상주). 기본 ON.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>메인창을 최소화하면 트레이로 숨김. 기본 OFF.</summary>
    public bool MinimizeToTrayOnMinimize { get; set; }
}
