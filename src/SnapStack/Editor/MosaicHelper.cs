using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapStack.Editor;

/// <summary>
/// 모자이크(픽셀화) 헬퍼(EDT-07). 베이스 이미지의 지정 영역을 블록 평균색으로 픽셀화한 타일을
/// 반환한다. 반환물은 오버레이 Canvas 위에 Image로 얹혀 합성된다(원본 픽셀 비파괴).
/// </summary>
public static class MosaicHelper
{
    /// <summary>
    /// <paramref name="source"/>의 <paramref name="regionPx"/>(원본 픽셀 좌표) 영역을
    /// <paramref name="cellSize"/> 픽셀 블록 평균색으로 픽셀화한 Frozen 비트맵을 만든다.
    /// 영역이 이미지 밖이면 교집합으로 클램프, 유효 영역 없으면 null.
    /// </summary>
    public static BitmapSource? Pixelate(BitmapSource source, Int32Rect regionPx, int cellSize)
    {
        // 원본 픽셀 경계로 클램프
        int sx = Math.Max(0, regionPx.X);
        int sy = Math.Max(0, regionPx.Y);
        int ex = Math.Min(source.PixelWidth, regionPx.X + regionPx.Width);
        int ey = Math.Min(source.PixelHeight, regionPx.Y + regionPx.Height);
        int w = ex - sx, h = ey - sy;
        if (w <= 0 || h <= 0) return null;
        if (cellSize < 2) cellSize = 2;

        // 베이스를 Bgra32로 정규화해 영역 픽셀을 읽는다.
        var fmt = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = w * 4;
        var pixels = new byte[h * stride];
        fmt.CopyPixels(new Int32Rect(sx, sy, w, h), pixels, stride, 0);

        // 블록 단위 평균색으로 덮어쓴다(픽셀화).
        for (int by = 0; by < h; by += cellSize)
        {
            int bh = Math.Min(cellSize, h - by);
            for (int bx = 0; bx < w; bx += cellSize)
            {
                int bw = Math.Min(cellSize, w - bx);
                long ab = 0, ag = 0, ar = 0, aa = 0;
                int count = bw * bh;

                for (int y = 0; y < bh; y++)
                {
                    int row = (by + y) * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int p = row + x * 4;
                        ab += pixels[p];
                        ag += pixels[p + 1];
                        ar += pixels[p + 2];
                        aa += pixels[p + 3];
                    }
                }

                byte mb = (byte)(ab / count), mg = (byte)(ag / count),
                     mr = (byte)(ar / count), ma = (byte)(aa / count);

                for (int y = 0; y < bh; y++)
                {
                    int row = (by + y) * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int p = row + x * 4;
                        pixels[p] = mb;
                        pixels[p + 1] = mg;
                        pixels[p + 2] = mr;
                        pixels[p + 3] = ma;
                    }
                }
            }
        }

        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
