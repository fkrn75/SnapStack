using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapStack.Services;

/// <summary>
/// 클립보드 복사/받기 구현 — PNG + 표준 Bitmap(DIB) 다중 포맷 등록으로 외부 앱 호환성 확보. §4.2.
/// (CF_DIBV5 알파 경로는 v1 불투명 전제상 생략, 향후 투명 도입 시 추가 — §4.3 각주.)
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public bool CopyImage(BitmapSource image)
    {
        try
        {
            var data = new DataObject();

            // ① PNG — 투명 보존, 최신 Office/그림판 선호(소비 측이 우선 선택).
            using var pngStream = new MemoryStream();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(image));
            enc.Save(pngStream);
            data.SetData("PNG", pngStream);

            // ② 표준 Bitmap/DIB — 광범위 호환(구형 앱·메신저).
            data.SetImage(image);

            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch
        {
            return false; // 호출 측이 토스트로 실패 통지(§4.0 서비스 API)
        }
    }

    public BitmapSource? TryGetImage()
    {
        try
        {
            // PNG 우선(투명 보존).
            if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is MemoryStream ms)
            {
                ms.Position = 0;
                var dec = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = dec.Frames[0];
                frame.Freeze();
                return frame;
            }
            // 폴백: 표준 이미지.
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                img?.Freeze();
                return img;
            }
        }
        catch { /* 클립보드 접근 실패 무시 */ }
        return null;
    }
}
