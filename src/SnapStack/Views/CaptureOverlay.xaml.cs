using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SnapStack.Views;

/// <summary>
/// 영역 선택 오버레이(CAP-07). 가상화면 전체를 덮고 드래그로 사각 영역을 선택한다.
/// 확정 시 <b>물리 픽셀</b> 좌표(Int32Rect)를 반환(취소 시 null).
/// </summary>
public partial class CaptureOverlay : Window
{
    private Point _start;
    private bool _dragging;
    private Int32Rect? _result;

    // 가상화면 원점(물리 픽셀) 및 DIP→물리 배율
    private int _vx, _vy;
    private double _dipToPx = 1.0;

    public CaptureOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    /// <summary>오버레이를 띄워 영역을 선택받는다. 비동기로 결과(물리 Int32Rect)를 반환.</summary>
    public static Task<Int32Rect?> SelectRegionAsync()
    {
        var tcs = new TaskCompletionSource<Int32Rect?>();
        var win = new CaptureOverlay();
        win.Closed += (_, _) => tcs.SetResult(win._result);
        win.Show();
        win.Activate();
        return tcs.Task;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var (vx, vy, vw, vh) = Interop.ScreenMetrics.VirtualScreen();
        _vx = vx;
        _vy = vy;

        // 현재 DIP→물리 배율(MVP: 단일 배율 가정. 혼합 DPI 정밀도는 향후 CAP-06 보강).
        var src = PresentationSource.FromVisual(this);
        _dipToPx = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        // 물리 가상화면을 DIP로 환산해 창을 정확히 덮는다.
        Left = vx / _dipToPx;
        Top = vy / _dipToPx;
        Width = vw / _dipToPx;
        Height = vh / _dipToPx;

        Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _result = null;
            Close();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _start = e.GetPosition(RootCanvas);
        _dragging = true;
        SelectionRect.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;

        var p = e.GetPosition(RootCanvas);
        double x = Math.Min(p.X, _start.X), y = Math.Min(p.Y, _start.Y);
        double w = Math.Abs(p.X - _start.X), h = Math.Abs(p.Y - _start.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        // 실시간 치수(물리 픽셀 환산)
        SizeText.Text = $"{(int)Math.Round(w * _dipToPx)} × {(int)Math.Round(h * _dipToPx)}";
        Canvas.SetLeft(SizeLabel, x);
        Canvas.SetTop(SizeLabel, Math.Max(0, y - 24));
        SizeLabel.Visibility = Visibility.Visible;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var p = e.GetPosition(RootCanvas);
        double dx = Math.Min(p.X, _start.X), dy = Math.Min(p.Y, _start.Y);
        double dw = Math.Abs(p.X - _start.X), dh = Math.Abs(p.Y - _start.Y);

        if (dw < 2 || dh < 2)
        {
            _result = null; // 너무 작은 선택은 취소로 간주
        }
        else
        {
            // DIP → 물리 픽셀 + 가상화면 원점 오프셋
            int px = _vx + (int)Math.Round(dx * _dipToPx);
            int py = _vy + (int)Math.Round(dy * _dipToPx);
            int pw = (int)Math.Round(dw * _dipToPx);
            int ph = (int)Math.Round(dh * _dipToPx);
            _result = new Int32Rect(px, py, pw, ph);
        }
        Close();
    }
}
