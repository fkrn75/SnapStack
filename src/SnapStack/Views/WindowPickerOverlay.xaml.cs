using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SnapStack.Interop;

namespace SnapStack.Views;

/// <summary>
/// 창 선택 오버레이(CAP-02). 가상화면을 거의 투명하게 덮고, 커서 아래의 <b>최상위 창</b>을
/// 실시간 하이라이트한다. 클릭 시 그 창의 핸들(HWND)을 반환(Esc/우클릭/빈 곳 = IntPtr.Zero).
/// </summary>
public partial class WindowPickerOverlay : Window
{
    private IntPtr _selfHwnd;
    private IntPtr _hover = IntPtr.Zero;   // 현재 가리킨 창
    private IntPtr _result = IntPtr.Zero;  // 확정 결과

    private int _vx, _vy;                  // 가상화면 원점(물리 픽셀)
    private double _dipToPx = 1.0;

    public WindowPickerOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    /// <summary>오버레이를 띄워 창을 선택받는다. 비동기로 대상 창 HWND를 반환(취소 시 Zero).</summary>
    public static Task<IntPtr> SelectWindowAsync()
    {
        var tcs = new TaskCompletionSource<IntPtr>();
        var win = new WindowPickerOverlay();
        win.Closed += (_, _) => tcs.SetResult(win._result);
        win.Show();
        win.Activate();
        return tcs.Task;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _selfHwnd = new WindowInteropHelper(this).Handle;

        var (vx, vy, vw, vh) = ScreenMetrics.VirtualScreen();
        _vx = vx; _vy = vy;

        var src = PresentationSource.FromVisual(this);
        _dipToPx = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        Left = vx / _dipToPx; Top = vy / _dipToPx;
        Width = vw / _dipToPx; Height = vh / _dipToPx;

        Focus();
        UpdateHover();   // 초기 커서 위치 반영
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _result = IntPtr.Zero; Close(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateHover();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _result = _hover;       // 현재 가리킨 창 확정(없으면 Zero = 취소)
        Close();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        _result = IntPtr.Zero;  // 우클릭 = 취소
        Close();
    }

    /// <summary>커서 아래 최상위 창을 찾아 하이라이트를 갱신.</summary>
    private void UpdateHover()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;

        _hover = FindWindowAt(pt);

        if (_hover == IntPtr.Zero || !TryGetVisibleBounds(_hover, out var r))
        {
            HighlightRect.Visibility = Visibility.Collapsed;
        }
        else
        {
            // 물리 → 오버레이 로컬 DIP (물리 - 가상원점, DIP 환산)
            Canvas.SetLeft(HighlightRect, (r.Left - _vx) / _dipToPx);
            Canvas.SetTop(HighlightRect, (r.Top - _vy) / _dipToPx);
            HighlightRect.Width = Math.Max(0, r.Width / _dipToPx);
            HighlightRect.Height = Math.Max(0, r.Height / _dipToPx);
            HighlightRect.Visibility = Visibility.Visible;
        }

        // 힌트 라벨을 커서 근처로
        var mp = Mouse.GetPosition(RootCanvas);
        Canvas.SetLeft(HintLabel, mp.X + 16);
        Canvas.SetTop(HintLabel, mp.Y + 16);
    }

    /// <summary>Z순서 상위부터 커서를 포함하는 가시·비클록·비최소화 창을 찾는다(자기 자신·셸 제외).</summary>
    private IntPtr FindWindowAt(NativeMethods.POINT pt)
    {
        IntPtr found = IntPtr.Zero;
        IntPtr shell = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == _selfHwnd || hWnd == shell) return true;     // 오버레이·바탕화면 제외
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.IsIconic(hWnd)) return true;           // 최소화 제외
            if (IsCloaked(hWnd)) return true;                        // 클록(타 가상데스크탑) 제외
            if (!TryGetVisibleBounds(hWnd, out var r)) return true;
            if (r.Width < 8 || r.Height < 8) return true;            // 너무 작은 창 제외

            if (pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom)
            {
                found = hWnd;
                return false;   // Z순서 최상위 매칭 → 열거 중단
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static bool IsCloaked(IntPtr hWnd) =>
        NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
        && cloaked != 0;

    /// <summary>드롭섀도 제외 가시 경계(DWMWA_EXTENDED_FRAME_BOUNDS). 실패 시 GetWindowRect.</summary>
    private static bool TryGetVisibleBounds(IntPtr hWnd, out NativeMethods.RECT r)
    {
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out r, Marshal.SizeOf<NativeMethods.RECT>()) == 0 && r.Width > 0 && r.Height > 0)
            return true;
        return NativeMethods.GetWindowRect(hWnd, out r) && r.Width > 0 && r.Height > 0;
    }
}
