using System;
using System.ComponentModel;
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
    private readonly ISettingsService _settings;

    private bool _reallyExit;     // 트레이 '종료' 또는 트레이 상주 OFF에서의 진짜 종료 플래그
    private bool _balloonShown;   // 첫 트레이 최소화 안내 풍선을 한 번만 표시

    public MainWindow(MainViewModel viewModel, IHotkeyService hotkeys, ITrayService tray, ISettingsService settings)
    {
        InitializeComponent();
        _vm = viewModel;
        _hotkeys = hotkeys;
        _tray = tray;
        _settings = settings;
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
        // 서비스 Dispose는 App.OnExit(DI 컨테이너 정리)로 일임 — 트레이로 Hide만 된 경우 Dispose하면 안 되므로.
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        _tray.CommandRequested -= OnTrayCommand;
    }

    // ── 트레이 상주(close-to-tray) ──
    // X 닫기: 트레이 상주가 켜져 있고 '진짜 종료'가 아니면 종료 대신 트레이로 숨긴다.
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_reallyExit) return; // 진짜 종료 경로 — 그대로 닫힘

        if (_settings.Current.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            // 트레이 상주 OFF: 닫기 = 앱 종료(OnExplicitShutdown이라 명시적으로 종료해야 함).
            _reallyExit = true;
            Application.Current.Shutdown();
        }
    }

    // 최소화 시 트레이로 숨김(옵션).
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _settings.Current.MinimizeToTrayOnMinimize)
            HideToTray();
    }

    // 창을 트레이로 숨기고 첫 회에 한해 안내 풍선을 띄운다.
    private void HideToTray()
    {
        Hide();
        if (!_balloonShown)
        {
            _balloonShown = true;
            _tray.ShowBalloon("SnapStack", "트레이에서 계속 실행 중입니다. 아이콘을 두 번 클릭하면 다시 열립니다.");
        }
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
                _reallyExit = true;     // OnClosing이 닫기를 취소하지 않도록
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
