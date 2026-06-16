using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnapStack.Models;
using SnapStack.Services;

namespace SnapStack.ViewModels;

/// <summary>
/// 메인 화면 뷰모델 — 캡쳐 명령 / 히스토리 바인딩 / 미리보기 / 복사·저장. (MVP 관통)
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ICaptureService _capture;
    private readonly IHistoryService _history;
    private readonly IClipboardService _clipboard;
    private readonly IFileExportService _file;

    /// <summary>히스토리(순서대로). ListBox에 바인딩. §4.1.</summary>
    public ReadOnlyObservableCollection<CaptureItem> History => _history.Items;

    [ObservableProperty] private CaptureItem? _selected;
    [ObservableProperty] private BitmapSource? _preview;
    [ObservableProperty] private string _status = "캡쳐를 시작하세요";

    public MainViewModel(ICaptureService capture, IHistoryService history,
                         IClipboardService clipboard, IFileExportService file)
    {
        _capture = capture;
        _history = history;
        _clipboard = clipboard;
        _file = file;

        // 히스토리 측 선택 변경(새 캡쳐 등) → 뷰모델 선택 동기화
        _history.SelectionChanged += item =>
        {
            if (!ReferenceEquals(Selected, item)) Selected = item;
        };
    }

    // 뷰모델 선택 변경 → 미리보기 갱신 + 히스토리 동기화(루프 방지 가드)
    partial void OnSelectedChanged(CaptureItem? value)
    {
        Preview = value?.EffectiveImage;
        if (!ReferenceEquals(_history.Selected, value)) _history.Selected = value;
    }

    /// <summary>편집기에서 MarkEdited 후 미리보기를 최신 EffectiveImage로 갱신(편집본 반영). HIS-03.</summary>
    public void RefreshSelectedPreview() => Preview = Selected?.EffectiveImage;

    [RelayCommand]
    private async Task CaptureRegion()
    {
        Status = "영역을 드래그하세요 (Esc 취소)...";
        var item = await _capture.CaptureRegionAsync();
        Status = item is null ? "캡쳐 취소됨" : $"영역 캡쳐됨 (#{item.Sequence})";
    }

    [RelayCommand]
    private void CaptureFullScreen()
    {
        var item = _capture.CaptureFullScreen(false);
        Status = $"전체화면 캡쳐됨 (#{item.Sequence})";
    }

    [RelayCommand]
    private async Task CaptureWindow()
    {
        var item = await _capture.CaptureWindowAsync();
        Status = item is null ? "캡쳐 취소됨" : $"캡쳐됨 (#{item.Sequence})";
    }

    [RelayCommand]
    private async Task CaptureDelayed()
    {
        Status = "3초 후 전체화면 캡쳐...";
        var item = await _capture.CaptureDelayedAsync(3, CaptureMode.FullScreen);
        Status = item is null ? "캡쳐 취소됨" : $"지연 캡쳐됨 (#{item.Sequence})";
    }

    [RelayCommand]
    private void Copy()
    {
        if (Selected is null) { Status = "복사할 항목이 없습니다"; return; }
        Status = _clipboard.CopyImage(Selected.EffectiveImage)
            ? "클립보드에 복사됨 — 다른 앱에서 Ctrl+V" : "복사 실패";
    }

    [RelayCommand]
    private void Save()
    {
        if (Selected is null) { Status = "저장할 항목이 없습니다"; return; }
        var path = _file.QuickSave(Selected.EffectiveImage);
        Status = $"저장됨: {path}";
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is { } s) { _history.Remove(s); Status = "삭제됨"; }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _history.Clear();
        Status = "히스토리 비움";
    }
}
