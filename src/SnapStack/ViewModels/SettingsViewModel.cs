using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnapStack.Models;
using SnapStack.Services;

namespace SnapStack.ViewModels;

/// <summary>
/// 단축키 1행(동작명 + 제스처). 설정창에서 HotkeyCaptureBox로 재지정 가능(양방향).
/// <see cref="Action"/>은 저장 키(불변), <see cref="Label"/>은 표시용 한글 라벨,
/// <see cref="Gesture"/>는 캡쳐된 제스처 문자열(편집 대상).
/// </summary>
public sealed partial class HotkeyRow : ObservableObject
{
    /// <summary>AppSettings.Hotkeys 딕셔너리 키(동작 식별자, 불변).</summary>
    public required string Action { get; init; }

    /// <summary>UI 표시용 한글 라벨(없으면 Action 그대로).</summary>
    public required string Label { get; init; }

    /// <summary>캡쳐된 제스처 문자열(예: "Ctrl+Shift+W"). HotkeyCaptureBox와 양방향 바인딩.</summary>
    [ObservableProperty] private string _gesture = string.Empty;
}

/// <summary>
/// 설정창 뷰모델(§5 SYS-04). <see cref="AppSettings"/>를 편집하고 저장 시 영속화한다.
/// 편집 항목: 저장 폴더·기본 포맷·지연 초·캡쳐 후 편집기 열기·시작 시 자동 실행·
/// 히스토리·전역 단축키 재지정(SYS-04)·트레이 상주 옵션.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IHotkeyService? _hotkeys;

    // ── 편집 가능한 설정 항목(현재 값으로 초기화) ──
    [ObservableProperty] private string _saveFolder;
    [ObservableProperty] private ImageFormatKind _defaultFormat;
    [ObservableProperty] private int _delaySeconds;
    [ObservableProperty] private bool _openEditorAfterCapture;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _persistHistory;
    [ObservableProperty] private int _historyLimit;

    // ── 트레이 상주 옵션 ──
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _minimizeToTrayOnMinimize;

    /// <summary>저장 포맷 콤보박스 항목.</summary>
    public IReadOnlyList<ImageFormatKind> FormatOptions { get; } =
        new[] { ImageFormatKind.Png, ImageFormatKind.Jpg, ImageFormatKind.Bmp };

    /// <summary>단축키 목록(동작명 → 제스처). HotkeyCaptureBox로 재지정 가능.</summary>
    public ObservableCollection<HotkeyRow> Hotkeys { get; } = new();

    /// <summary>동작 식별자 → UI 표시용 한글 라벨(§5.1 마스터표).</summary>
    private static readonly IReadOnlyDictionary<string, string> ActionLabels =
        new Dictionary<string, string>
        {
            ["CaptureRegion"]      = "영역 캡쳐",
            ["CaptureWindow"]      = "창 캡쳐",
            ["CaptureFullScreen"]  = "전체 화면 캡쳐",
            ["CaptureVirtualFull"] = "가상 전체 화면 캡쳐",
            ["CaptureLastRegion"]  = "마지막 영역 재캡쳐",
            ["CaptureDelayed"]     = "지연 캡쳐",
        };

    /// <summary>저장 후 창을 닫도록 뷰에 요청(true=저장 완료). 뷰가 구독해 DialogResult/Close 처리.</summary>
    public event Action? Saved;

    /// <summary>같은 제스처가 둘 이상 지정돼 저장이 거부됨(인자=중복 제스처). 뷰가 구독해 사용자 알림.</summary>
    public event Action<string>? HotkeyConflict;

    /// <summary>저장 후 일부 단축키 등록 실패(타 앱 선점 등). 인자=실패한 동작명 목록.</summary>
    public event Action<IReadOnlyList<string>>? HotkeyApplyFailed;

    public SettingsViewModel(ISettingsService settings, IHotkeyService? hotkeys = null)
    {
        _settings = settings;
        _hotkeys = hotkeys;

        var s = settings.Current;
        _saveFolder = s.SaveFolder;
        _defaultFormat = s.DefaultFormat;
        _delaySeconds = s.DelaySeconds;
        _openEditorAfterCapture = s.OpenEditorAfterCapture;
        _runAtStartup = s.RunAtStartup;
        _persistHistory = s.PersistHistory;
        _historyLimit = s.HistoryLimit;
        _minimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        _minimizeToTrayOnMinimize = s.MinimizeToTrayOnMinimize;

        foreach (var kv in s.Hotkeys.OrderBy(k => k.Key))
            Hotkeys.Add(new HotkeyRow
            {
                Action = kv.Key,
                Label = ActionLabels.TryGetValue(kv.Key, out var lbl) ? lbl : kv.Key,
                Gesture = kv.Value,
            });
    }

    /// <summary>편집 값을 AppSettings에 반영하고 영속화한 뒤 Saved 이벤트로 창 닫기를 요청.</summary>
    [RelayCommand]
    private void Save()
    {
        var s = _settings.Current;

        // ── 단축키 중복 검증 먼저 — 충돌이면 아무것도 저장하지 않는다(원자적) ──
        // 빈 제스처는 '미지정'이라 중복 검사 제외.
        var dup = Hotkeys
            .Where(r => !string.IsNullOrWhiteSpace(r.Gesture))
            .GroupBy(r => r.Gesture, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
        {
            HotkeyConflict?.Invoke(dup.Key);
            return; // 저장 중단(창 유지) — UI 상태(체크박스·다른 단축키)는 보존됨
        }

        s.SaveFolder = SaveFolder;
        s.DefaultFormat = DefaultFormat;
        s.DelaySeconds = DelaySeconds;
        s.OpenEditorAfterCapture = OpenEditorAfterCapture;
        s.RunAtStartup = RunAtStartup;
        s.PersistHistory = PersistHistory;
        s.HistoryLimit = HistoryLimit;
        s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        s.MinimizeToTrayOnMinimize = MinimizeToTrayOnMinimize;

        // 단축키 역반영(동작명 → 캡쳐된 제스처 문자열)
        foreach (var row in Hotkeys)
            s.Hotkeys[row.Action] = row.Gesture ?? string.Empty;

        _settings.Save();

        // 저장된 설정으로 전역 핫키 즉시 재적용(실패 키는 알림). 핫키 서비스 미주입 시 생략.
        var failed = _hotkeys?.Reapply(s);
        if (failed is { Count: > 0 })
            HotkeyApplyFailed?.Invoke(failed);

        Saved?.Invoke();
    }
}
