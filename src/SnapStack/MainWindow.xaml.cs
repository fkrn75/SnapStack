using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using SnapStack.Services;
using SnapStack.ViewModels;
using SnapStack.Views;

namespace SnapStack;

/// <summary>
/// 메인 윈도우 — 캡쳐 도구바 + 히스토리 스트립 + 미리보기.
/// 전역 단축키(SYS-01)·시스템 트레이(SYS-02)를 초기화하고,
/// 히스토리 더블클릭/편집 버튼으로 편집기(EDT)를, 설정 버튼으로 설정창(SYS-04)을 연다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly IHotkeyService _hotkeys;
    private readonly ITrayService _tray;

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeys, ITrayService tray)
    {
        InitializeComponent();
        _vm = viewModel;
        _hotkeys = hotkeys;
        _tray = tray;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    // 윈도우 핸들이 생긴 Loaded 시점에 전역 단축키·트레이 초기화
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        _hotkeys.Initialize(hwnd);

        _tray.CommandRequested += OnTrayCommand;
        _tray.Initialize(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        _tray.CommandRequested -= OnTrayCommand;
        _hotkeys.Dispose();
        _tray.Dispose();
    }

    // ── 전역 단축키 / 트레이 동작 → 캡쳐 커맨드 디스패치 ──
    private void OnHotkeyPressed(string action) => Dispatch(action);

    private void OnTrayCommand(string action)
    {
        switch (action)
        {
            case "ShowMain":
                Show();
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                break;
            case "Exit":
                Application.Current.Shutdown();
                break;
            default:
                Dispatch(action);
                break;
        }
    }

    // 동작명 → MainViewModel 캡쳐 커맨드(존재하는 것만 실행). 미구현 동작은 무시.
    private void Dispatch(string action)
    {
        switch (action)
        {
            case "CaptureRegion": Run(_vm.CaptureRegionCommand); break;
            case "CaptureWindow": Run(_vm.CaptureWindowCommand); break;
            case "CaptureFullScreen": Run(_vm.CaptureFullScreenCommand); break;
            case "CaptureDelayed": Run(_vm.CaptureDelayedCommand); break;
            // CaptureVirtualFull / CaptureLastRegion: 전용 커맨드 미구현(후속) — 무시
        }
    }

    private static void Run(ICommand cmd)
    {
        if (cmd.CanExecute(null)) cmd.Execute(null);
    }

    // ── 편집기(EDT) — 히스토리 더블클릭 / 편집 버튼 ──
    private void History_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenEditorForSelected();
    private void OpenEditor_Click(object sender, RoutedEventArgs e) => OpenEditorForSelected();

    private void OpenEditorForSelected()
    {
        if (_vm.Selected is not { } item)
        {
            _vm.Status = "편집할 항목을 선택하세요";
            return;
        }

        var sp = App.Services;
        var evm = new EditorViewModel(
            item,
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IFileExportService>(),
            sp.GetRequiredService<IHistoryService>());

        var win = new EditorWindow(evm) { Owner = this };
        win.ShowDialog();

        // 편집본(EditedComposite)이 채워졌으면 미리보기 갱신
        _vm.RefreshSelectedPreview();
    }

    // ── 설정창(SYS-04) ──
    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<SettingsWindow>();
        win.Owner = this;
        win.ShowDialog();
    }
}
