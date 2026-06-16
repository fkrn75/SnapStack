using System.Windows;
using System.Windows.Media;

namespace SnapStack.Editor;

/// <summary>
/// 화살표 도형 생성 헬퍼(EDT-04). 시작→끝 선분 + 끝점 머리(삼각형 두 날개)를
/// 하나의 PathGeometry로 만든다. 굵기에 비례한 머리 크기.
/// </summary>
public static class ArrowGeometry
{
    /// <summary>
    /// <paramref name="from"/>→<paramref name="to"/> 화살표의 Geometry를 만든다.
    /// <paramref name="thickness"/>에 비례해 머리 크기를 정한다(최소 보장).
    /// </summary>
    public static Geometry Build(Point from, Point to, double thickness)
    {
        var geo = new StreamGeometry();
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);

        using (var ctx = geo.Open())
        {
            // 몸통 선
            ctx.BeginFigure(from, isFilled: false, isClosed: false);
            ctx.LineTo(to, isStroked: true, isSmoothJoin: false);

            if (len > 0.5)
            {
                // 머리 크기: 굵기의 4배 + 기본치, 선 길이의 절반으로 상한
                double head = Math.Min(len * 0.5, thickness * 4 + 8);
                double ux = dx / len, uy = dy / len;          // 진행 단위벡터
                double angle = Math.PI / 7;                    // 머리 벌어짐(약 25°)
                double cos = Math.Cos(angle), sin = Math.Sin(angle);

                // 끝점에서 뒤로 돌린 두 날개 점
                var wing1 = new Point(
                    to.X - head * (ux * cos - uy * sin),
                    to.Y - head * (uy * cos + ux * sin));
                var wing2 = new Point(
                    to.X - head * (ux * cos + uy * sin),
                    to.Y - head * (uy * cos - ux * sin));

                ctx.BeginFigure(to, isFilled: false, isClosed: false);
                ctx.LineTo(wing1, isStroked: true, isSmoothJoin: false);
                ctx.BeginFigure(to, isFilled: false, isClosed: false);
                ctx.LineTo(wing2, isStroked: true, isSmoothJoin: false);
            }
        }
        geo.Freeze();
        return geo;
    }
}
