using System.Drawing;
using System.Runtime.InteropServices;
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

    // 창 캡쳐(CAP-02): 오버레이로 창을 가리켜 클릭 → 그 창만 PrintWindow로 캡쳐(가려져도 OK).
    public async Task<CaptureItem?> CaptureWindowAsync()
    {
        var hwnd = await Views.WindowPickerOverlay.SelectWindowAsync();
        if (hwnd == IntPtr.Zero) return null;

        await Task.Delay(80); // 오버레이가 완전히 사라진 뒤(폴백 BitBlt 대비)
        var bmp = CaptureWindow(hwnd);
        if (bmp is null) return null;

        return _history.Add(bmp, new System.Windows.Size(bmp.PixelWidth, bmp.PixelHeight),
            DateTime.Now, CaptureMode.Window);
    }

    /// <summary>
    /// 창 핸들을 PrintWindow(PW_RENDERFULLCONTENT)로 캡쳐하고 드롭섀도 영역을 크롭한다.
    /// PrintWindow 실패 시 가시 경계 화면 BitBlt로 폴백.
    /// </summary>
    private static BitmapSource? CaptureWindow(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var full) || full.Width <= 0 || full.Height <= 0)
            return null;
        int fw = full.Width, fh = full.Height;

        // 드롭섀도 제외 가시 경계(크롭 기준)
        bool hasVis = NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT vis, Marshal.SizeOf<NativeMethods.RECT>()) == 0 && vis.Width > 0 && vis.Height > 0;
        if (!hasVis) vis = full;

        using var bmpFull = new Bitmap(fw, fh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        bool ok;
        using (var g = Graphics.FromImage(bmpFull))
        {
            IntPtr hdc = g.GetHdc();
            try { ok = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT); }
            finally { g.ReleaseHdc(hdc); }
        }

        if (ok)
        {
            // GetWindowRect 공간에서 가시 경계만큼 크롭(섀도 여백 제거)
            int ox = vis.Left - full.Left, oy = vis.Top - full.Top;
            int cw = Math.Min(vis.Width, fw - ox), ch = Math.Min(vis.Height, fh - oy);
            if (hasVis && ox >= 0 && oy >= 0 && cw > 0 && ch > 0 && (ox > 0 || oy > 0 || cw != fw || ch != fh))
            {
                using var cropped = bmpFull.Clone(new Rectangle(ox, oy, cw, ch), bmpFull.PixelFormat);
                return ToBitmapSource(cropped);
            }
            return ToBitmapSource(bmpFull);
        }

        // 폴백: 가시 경계 화면 BitBlt(가려져 있으면 부정확할 수 있음)
        return CaptureRect(vis.Left, vis.Top, vis.Width, vis.Height);
    }

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
