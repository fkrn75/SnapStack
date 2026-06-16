using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 파일 저장/내보내기 구현 — EffectiveImage를 PNG/JPG/BMP로 인코딩. §4.4.
/// </summary>
public sealed class FileExportService : IFileExportService
{
    private readonly ISettingsService _settings;
    private int _seq;

    public FileExportService(ISettingsService settings) => _settings = settings;

    public bool Save(BitmapSource image, string path, ImageFormatKind format)
    {
        try
        {
            // JPG/BMP는 알파 미지원 → 흰색 매트 합성(SAV-02).
            var src = format == ImageFormatKind.Png ? image : FlattenToWhite(image);

            BitmapEncoder enc = format switch
            {
                ImageFormatKind.Jpg => new JpegBitmapEncoder { QualityLevel = 92 },
                ImageFormatKind.Bmp => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
            enc.Frames.Add(BitmapFrame.Create(src));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var fs = new FileStream(path, FileMode.Create);
            enc.Save(fs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string QuickSave(BitmapSource image)
    {
        var s = _settings.Current;
        Directory.CreateDirectory(s.SaveFolder);
        var name = BuildFileName(s.FileNameTemplate, s.DefaultFormat);
        var path = Path.Combine(s.SaveFolder, name);
        Save(image, path, s.DefaultFormat);
        return path;
    }

    /// <summary>파일명 템플릿 토큰 치환(SAV-03). 같은 초 충돌 방지용 seq.</summary>
    private string BuildFileName(string template, ImageFormatKind fmt)
    {
        var now = DateTime.Now;
        int seq = (++_seq) % 1000;
        var baseName = template
            .Replace("{yyyyMMdd}", now.ToString("yyyyMMdd"))
            .Replace("{HHmmss}", now.ToString("HHmmss"))
            .Replace("{seq:000}", seq.ToString("000"));
        return baseName + "." + fmt.ToString().ToLowerInvariant();
    }

    /// <summary>투명 이미지를 흰색 배경 위에 합성(JPG/BMP용).</summary>
    private static BitmapSource FlattenToWhite(BitmapSource src)
    {
        var rtb = new RenderTargetBitmap(src.PixelWidth, src.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, src.PixelWidth, src.PixelHeight);
            dc.DrawRectangle(Brushes.White, null, rect);
            dc.DrawImage(src, rect);
        }
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
