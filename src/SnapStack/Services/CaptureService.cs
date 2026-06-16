using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SnapStack.Interop;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 화면 캡쳐 구현(§2). GDI BitBlt(Graphics.CopyFromScreen)로 가상화면 물리 좌표를 캡쳐하고
/// 히스토리에 적재한다. 캡쳐 결과는 Frozen BitmapSource(§4.0 적재 계약).
/// </summary>
public sealed class CaptureService : ICaptureService
{
    private readonly IHistoryService _history;
    private Int32Rect? _lastRegion;       // 직전 영역(물리 픽셀)

    public CaptureService(IHistoryService history) => _history = history;

    public CaptureItem CaptureFullScreen(bool virtualFull = false)
    {
        var (x, y, w, h) = virtualFull ? ScreenMetrics.VirtualScreen() : ScreenMetrics.PrimaryScreen();
        var bmp = CaptureRect(x, y, w, h);
        return _history.Add(bmp, new System.Windows.Size(w, h), DateTime.Now,
            virtualFull ? CaptureMode.VirtualFull : CaptureMode.FullScreen);
    }

    public async Task<CaptureItem?> CaptureRegionAsync()
    {
        var rect = await Views.CaptureOverlay.SelectRegionAsync();
        if (rect is not { } r || r.Width <= 0 || r.Height <= 0) return null;

        _lastRegion = r;
        await Task.Delay(60); // 오버레이가 화면에서 완전히 사라진 뒤 캡쳐(잔상 방지)
        var bmp = CaptureRect(r.X, r.Y, r.Width, r.Height);
        return _history.Add(bmp, new System.Windows.Size(r.Width, r.Height), DateTime.Now, CaptureMode.Region);
    }

    public CaptureItem? CaptureLastRegion()
    {
        if (_lastRegion is not { } r) return null;
        var bmp = CaptureRect(r.X, r.Y, r.Width, r.Height);
        return _history.Add(bmp, new System.Windows.Size(r.Width, r.Height), DateTime.Now, CaptureMode.LastRegion);
    }

    // MVP: 창 캡쳐는 영역 선택으로 대체(향후 WindowFromPoint 자동 인식). CAP-02.
    public Task<CaptureItem?> CaptureWindowAsync() => CaptureRegionAsync();

    public async Task<CaptureItem?> CaptureDelayedAsync(int seconds, CaptureMode mode)
    {
        if (seconds > 0) await Task.Delay(seconds * 1000);
        return mode switch
        {
            CaptureMode.Region => await CaptureRegionAsync(),
            CaptureMode.VirtualFull => CaptureFullScreen(true),
            _ => CaptureFullScreen(false),
        };
    }

    /// <summary>지정 물리 사각형을 BitBlt로 캡쳐 → Frozen BitmapSource.</summary>
    private static BitmapSource CaptureRect(int x, int y, int width, int height)
    {
        using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }
        return ToBitmapSource(bmp);
    }

    /// <summary>GDI Bitmap → WPF BitmapSource(Frozen). HBITMAP 누수 방지.</summary>
    private static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        IntPtr hbitmap = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            NativeMethods.DeleteObject(hbitmap);
        }
    }
}
