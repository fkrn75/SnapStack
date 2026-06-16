using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnapStack.Models;
using SnapStack.Services;

namespace SnapStack.ViewModels;

/// <summary>단축키 1행(동작명 + 제스처). 설정창 단축키 목록 표시용(읽기 전용 표시).</summary>
public sealed class HotkeyRow
{
    public required string Action { get; init; }
    public required string Gesture { get; init; }
}

/// <summary>
/// 설정창 뷰모델(§5 SYS-04). <see cref="AppSettings"/>를 편집하고 저장 시 영속화한다.
/// 편집 항목: 저장 폴더·기본 포맷·지연 초·캡쳐 후 편집기 열기·시작 시 자동 실행·단축키 목록(표시).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    // ── 편집 가능한 설정 항목(현재 값으로 초기화) ──
    [ObservableProperty] private string _saveFolder;
    [ObservableProperty] private ImageFormatKind _defaultFormat;
    [ObservableProperty] private int _delaySeconds;
    [ObservableProperty] private bool _openEditorAfterCapture;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _persistHistory;
    [ObservableProperty] private int _historyLimit;

    /// <summary>저장 포맷 콤보박스 항목.</summary>
    public IReadOnlyList<ImageFormatKind> FormatOptions { get; } =
        new[] { ImageFormatKind.Png, ImageFormatKind.Jpg, ImageFormatKind.Bmp };

    /// <summary>단축키 목록(동작명 → 제스처). v1은 표시 전용(재지정은 후속).</summary>
    public ObservableCollection<HotkeyRow> Hotkeys { get; } = new();

    /// <summary>저장 후 창을 닫도록 뷰에 요청(true=저장 완료). 뷰가 구독해 DialogResult/Close 처리.</summary>
    public event System.Action? Saved;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;

        var s = settings.Current;
        _saveFolder = s.SaveFolder;
        _defaultFormat = s.DefaultFormat;
        _delaySeconds = s.DelaySeconds;
        _openEditorAfterCapture = s.OpenEditorAfterCapture;
        _runAtStartup = s.RunAtStartup;
        _persistHistory = s.PersistHistory;
        _historyLimit = s.HistoryLimit;

        foreach (var kv in s.Hotkeys.OrderBy(k => k.Key))
            Hotkeys.Add(new HotkeyRow { Action = kv.Key, Gesture = kv.Value });
    }

    /// <summary>편집 값을 AppSettings에 반영하고 영속화한 뒤 Saved 이벤트로 창 닫기를 요청.</summary>
    [RelayCommand]
    private void Save()
    {
        var s = _settings.Current;
        s.SaveFolder = SaveFolder;
        s.DefaultFormat = DefaultFormat;
        s.DelaySeconds = DelaySeconds;
        s.OpenEditorAfterCapture = OpenEditorAfterCapture;
        s.RunAtStartup = RunAtStartup;
        s.PersistHistory = PersistHistory;
        s.HistoryLimit = HistoryLimit;

        _settings.Save();
        Saved?.Invoke();
    }
}
