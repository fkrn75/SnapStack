using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SnapStack.Editor;
using SnapStack.ViewModels;

namespace SnapStack.Views;

/// <summary>
/// 주석 편집기 윈도우(§3 EDT). 좌측 도구바·상단 옵션바·중앙 캔버스·하단 상태바.
/// 베이스 이미지 위에 InkCanvas(펜/형광펜)와 오버레이 Canvas(도형/텍스트/번호/모자이크/자르기)를
/// 겹쳐 그리며, "적용/복사/저장" 시 <see cref="FlattenToBitmap"/>으로 원본 픽셀 1:1 합성본을 만든다(§4.0 평탄화 계약).
///
/// 좌표 규약: 편집 캔버스는 <b>1 DIP = 원본 1 픽셀</b>로 고정(CanvasHost.Width/Height = 원본 픽셀).
/// 따라서 CanvasHost 기준 마우스 좌표가 곧 원본 픽셀 좌표이고, 평탄화는 같은 픽셀 크기·96dpi로 렌더해 1:1 보장.
/// </summary>
public partial class EditorWindow : Window
{
    private readonly EditorViewModel _vm;

    // 원본 픽셀 크기(= 편집 캔버스 DIP 크기). 자르기 시 갱신.
    private double _pixelW;
    private double _pixelH;

    // 드래그 상태(도형/직선/화살표/모자이크/크롭)
    private bool _dragging;
    private Point _dragStart;
    private Shape? _previewShape;     // 드래그 중 미리보기 도형
    private Path? _previewArrow;      // 화살표 미리보기

    // 크롭 오버레이(어둠 마스크 + 선택 사각형)
    private Rectangle? _cropRect;

    // 미리 정의된 색상 팔레트(옵션바)
    private static readonly Color[] Palette =
    {
        Colors.Red, Colors.Orange, Colors.Yellow, Colors.LimeGreen,
        Colors.DodgerBlue, Colors.MediumPurple, Colors.Black, Colors.White
    };

    public EditorWindow(EditorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // 베이스 이미지 적재 + 캔버스 픽셀 크기 고정
        var baseImg = vm.BaseImage;
        _pixelW = baseImg.PixelWidth;
        _pixelH = baseImg.PixelHeight;
        BaseImageView.Source = baseImg;
        ApplyCanvasSize();

        BuildColorSwatches();
        UpdateCurrentColorBox();
        SyncInkAttributes();
        UpdateInkEnabled();

        // 옵션 변화 → Ink 속성·색상 박스 동기화
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(EditorViewModel.Tool):
                    UpdateInkEnabled();
                    CancelDrag();           // 도구 바뀌면 진행 중 드래그/크롭 취소
                    RemoveCropOverlay();
                    break;
                case nameof(EditorViewModel.Thickness):
                case nameof(EditorViewModel.StrokeColor):
                    SyncInkAttributes();
                    UpdateCurrentColorBox();
                    break;
            }
        };

        // VM의 닫기/토스트 요청 처리
        vm.Flatten = FlattenToBitmap;     // 평탄화 델리게이트 주입(§4.0)
        vm.CloseRequested += result =>
        {
            // 모달(ShowDialog)로 열렸을 때만 DialogResult 설정 가능 — 비모달이면 예외 → 무시하고 닫기(NFR-05).
            try { DialogResult = result; }
            catch (InvalidOperationException) { /* Show()로 열린 경우 */ }
            Close();
        };
        vm.Notify += msg => CoordText.Text = msg;

        // InkCanvas 획 추가/삭제를 Undo 스택에 연결
        Ink.StrokeCollected += OnStrokeCollected;

        // 오버레이 Canvas 마우스 이벤트(도형/텍스트/번호/모자이크/크롭)
        OverlayCanvas.MouseLeftButtonDown += Overlay_MouseDown;
        OverlayCanvas.MouseMove += Overlay_MouseMove;
        OverlayCanvas.MouseLeftButtonUp += Overlay_MouseUp;

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Keyboard.Focus(this);
    }

    // ── 캔버스 크기/색상 UI ─────────────────────────────────────────

    /// <summary>CanvasHost를 현재 픽셀 크기(DIP=px)로 맞춘다. 모든 레이어가 1:1로 겹친다.</summary>
    private void ApplyCanvasSize()
    {
        CanvasHost.Width = _pixelW;
        CanvasHost.Height = _pixelH;
        Ink.Width = _pixelW; Ink.Height = _pixelH;
        OverlayCanvas.Width = _pixelW; OverlayCanvas.Height = _pixelH;
        RasterCanvas.Width = _pixelW; RasterCanvas.Height = _pixelH;
    }

    /// <summary>옵션바 색상 스와치 생성(클릭 시 StrokeColor 변경).</summary>
    private void BuildColorSwatches()
    {
        foreach (var c in Palette)
        {
            var b = new Button
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(1, 0, 1, 0),
                Background = new SolidColorBrush(c),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                BorderThickness = new Thickness(1),
                Tag = c
            };
            b.Click += (_, _) => { _vm.StrokeColor = (Color)b.Tag; };
            ColorSwatches.Items.Add(b);
        }
    }

    private void UpdateCurrentColorBox()
        => CurrentColorBox.Background = new SolidColorBrush(_vm.StrokeColor);

    // ── 펜/형광펜(InkCanvas) ────────────────────────────────────────

    /// <summary>활성 도구가 펜/형광펜일 때만 InkCanvas가 입력을 받게 한다.</summary>
    private void UpdateInkEnabled()
    {
        bool inkTool = _vm.Tool is EditorTool.Pen or EditorTool.Highlighter;
        Ink.EditingMode = inkTool ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        // Ink가 비활성일 때만 오버레이 Canvas가 마우스를 받는다(도형/텍스트/번호/모자이크/크롭).
        Ink.IsHitTestVisible = inkTool;
        OverlayCanvas.IsHitTestVisible = !inkTool;
        SyncInkAttributes();
    }

    /// <summary>현재 색·굵기·형광펜 여부를 DrawingAttributes에 반영(EDT-01/02).</summary>
    private void SyncInkAttributes()
    {
        bool hi = _vm.Tool == EditorTool.Highlighter;
        var color = _vm.StrokeColor;
        var attr = new DrawingAttributes
        {
            Color = color,
            Width = hi ? Math.Max(_vm.Thickness * 3, 12) : _vm.Thickness,
            Height = hi ? Math.Max(_vm.Thickness * 3, 12) : _vm.Thickness,
            IsHighlighter = hi,            // 형광펜: 곱하기 합성·반투명 느낌
            FitToCurve = true,             // 부드러운 곡선(EDT-01 Should)
            StylusTip = StylusTip.Ellipse
        };
        Ink.DefaultDrawingAttributes = attr;
    }

    // 획 1개 완성 → Undo 스택에 (제거/복원) 등록
    private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        var stroke = e.Stroke;
        _vm.Undo.Push(new EditAction
        {
            Label = "획",
            Undo = () => { if (Ink.Strokes.Contains(stroke)) Ink.Strokes.Remove(stroke); },
            Redo = () => { if (!Ink.Strokes.Contains(stroke)) Ink.Strokes.Add(stroke); }
        });
    }

    // ── 오버레이 Canvas: 도형/직선/화살표/텍스트/번호/모자이크/크롭 ─────

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(OverlayCanvas);

        switch (_vm.Tool)
        {
            case EditorTool.Text:
                PlaceTextBox(p);
                return;

            case EditorTool.Number:
                PlaceNumberStamp(p);
                return;

            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
            case EditorTool.Arrow:
            case EditorTool.Mosaic:
            case EditorTool.Crop:
                BeginDrag(p);
                OverlayCanvas.CaptureMouse();
                return;
        }
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(OverlayCanvas);
        CoordText.Text = $"{(int)Math.Round(p.X)}, {(int)Math.Round(p.Y)}";
        if (!_dragging) return;

        // Shift: 정사각/정원 또는 15°(직선/화살표) 스냅 — 명세 EDT-03/04
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var end = ConstrainPoint(_dragStart, p, shift);
        UpdateDragPreview(_dragStart, end);
    }

    private void Overlay_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        OverlayCanvas.ReleaseMouseCapture();
        var p = e.GetPosition(OverlayCanvas);
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var end = ConstrainPoint(_dragStart, p, shift);
        CommitDrag(_dragStart, end);
        _dragging = false;
    }

    private void BeginDrag(Point start)
    {
        _dragging = true;
        _dragStart = start;
        _previewShape = null;
        _previewArrow = null;

        if (_vm.Tool == EditorTool.Crop)
        {
            ShowCropOverlay(start);
        }
    }

    /// <summary>Shift 제약: 도형은 정사각/정원, 직선·화살표는 15° 스냅.</summary>
    private Point ConstrainPoint(Point start, Point cur, bool shift)
    {
        if (!shift) return cur;

        if (_vm.Tool is EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Crop or EditorTool.Mosaic)
        {
            double side = Math.Max(Math.Abs(cur.X - start.X), Math.Abs(cur.Y - start.Y));
            double nx = start.X + Math.Sign(cur.X - start.X) * side;
            double ny = start.Y + Math.Sign(cur.Y - start.Y) * side;
            return new Point(nx, ny);
        }
        if (_vm.Tool is EditorTool.Line or EditorTool.Arrow)
        {
            double dx = cur.X - start.X, dy = cur.Y - start.Y;
            double ang = Math.Atan2(dy, dx);
            double step = Math.PI / 12;                 // 15°
            double snapped = Math.Round(ang / step) * step;
            double len = Math.Sqrt(dx * dx + dy * dy);
            return new Point(start.X + len * Math.Cos(snapped), start.Y + len * Math.Sin(snapped));
        }
        return cur;
    }

    private void UpdateDragPreview(Point start, Point end)
    {
        switch (_vm.Tool)
        {
            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Mosaic:
                EnsurePreviewRectLike(end == start);
                PositionRectLike(_previewShape!, start, end);
                break;

            case EditorTool.Line:
                EnsurePreviewLine();
                var ln = (Line)_previewShape!;
                ln.X1 = start.X; ln.Y1 = start.Y; ln.X2 = end.X; ln.Y2 = end.Y;
                break;

            case EditorTool.Arrow:
                EnsurePreviewArrow();
                _previewArrow!.Data = ArrowGeometry.Build(start, end, _vm.Thickness);
                break;

            case EditorTool.Crop:
                UpdateCropOverlay(start, end);
                break;
        }
    }

    private void EnsurePreviewRectLike(bool _)
    {
        if (_previewShape is not null) return;
        var stroke = new SolidColorBrush(_vm.StrokeColor);
        bool mosaic = _vm.Tool == EditorTool.Mosaic;
        Shape s = _vm.Tool == EditorTool.Ellipse ? new Ellipse() : new Rectangle();
        s.Stroke = mosaic ? new SolidColorBrush(Color.FromArgb(180, 0, 122, 204)) : stroke;
        s.StrokeThickness = mosaic ? 1 : _vm.Thickness;
        if (mosaic) s.StrokeDashArray = new DoubleCollection { 4, 3 };
        if (!mosaic && _vm.FillEnabled) s.Fill = new SolidColorBrush(_vm.StrokeColor) { Opacity = 0.25 };
        _previewShape = s;
        OverlayCanvas.Children.Add(s);
    }

    private void EnsurePreviewLine()
    {
        if (_previewShape is not null) return;
        var ln = new Line
        {
            Stroke = new SolidColorBrush(_vm.StrokeColor),
            StrokeThickness = _vm.Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        _previewShape = ln;
        OverlayCanvas.Children.Add(ln);
    }

    private void EnsurePreviewArrow()
    {
        if (_previewArrow is not null) return;
        _previewArrow = new Path
        {
            Stroke = new SolidColorBrush(_vm.StrokeColor),
            StrokeThickness = _vm.Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        OverlayCanvas.Children.Add(_previewArrow);
    }

    private static void PositionRectLike(Shape s, Point a, Point b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        Canvas.SetLeft(s, x);
        Canvas.SetTop(s, y);
        s.Width = Math.Abs(a.X - b.X);
        s.Height = Math.Abs(a.Y - b.Y);
    }

    private void CommitDrag(Point start, Point end)
    {
        switch (_vm.Tool)
        {
            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
                CommitShape();
                break;
            case EditorTool.Arrow:
                CommitArrow();
                break;
            case EditorTool.Mosaic:
                CommitMosaic(start, end);
                break;
            case EditorTool.Crop:
                CommitCrop(start, end);
                break;
        }
    }

    // 도형/직선 확정: 너무 작으면 폐기, 아니면 Undo 등록
    private void CommitShape()
    {
        var shape = _previewShape;
        _previewShape = null;
        if (shape is null) return;

        bool tooSmall = shape is Line ln
            ? Math.Abs(ln.X2 - ln.X1) < 2 && Math.Abs(ln.Y2 - ln.Y1) < 2
            : shape.Width < 2 && shape.Height < 2;
        if (tooSmall) { OverlayCanvas.Children.Remove(shape); return; }

        PushChildAdd(shape, "도형");
    }

    private void CommitArrow()
    {
        var arrow = _previewArrow;
        _previewArrow = null;
        if (arrow is null) return;
        if (arrow.Data is null || arrow.Data.Bounds.IsEmpty ||
            (arrow.Data.Bounds.Width < 2 && arrow.Data.Bounds.Height < 2))
        {
            OverlayCanvas.Children.Remove(arrow);
            return;
        }
        PushChildAdd(arrow, "화살표");
    }

    // 모자이크 확정: 영역 픽셀화 결과를 RasterCanvas에 Image로 얹는다(EDT-07)
    private void CommitMosaic(Point start, Point end)
    {
        if (_previewShape is not null) { OverlayCanvas.Children.Remove(_previewShape); _previewShape = null; }

        int x = (int)Math.Round(Math.Min(start.X, end.X));
        int y = (int)Math.Round(Math.Min(start.Y, end.Y));
        int w = (int)Math.Round(Math.Abs(start.X - end.X));
        int h = (int)Math.Round(Math.Abs(start.Y - end.Y));
        if (w < 4 || h < 4) return;

        // 현재 표시 중인 베이스(크롭됐으면 이미 잘린 비트맵)의 픽셀 영역을 직접 픽셀화 — 캔버스 좌표=베이스 픽셀좌표.
        var currentBase = (BitmapSource)BaseImageView.Source;
        var region = new Int32Rect(x, y, w, h);
        int cell = Math.Max(6, (int)(_vm.Thickness * 2));   // 굵기에 비례한 셀 크기
        var tile = MosaicHelper.Pixelate(currentBase, region, cell);
        if (tile is null) return;

        var img = new Image { Source = tile, Width = w, Height = h, Stretch = Stretch.Fill };
        Canvas.SetLeft(img, x);
        Canvas.SetTop(img, y);
        RasterCanvas.Children.Add(img);

        _vm.Undo.Push(new EditAction
        {
            Label = "모자이크",
            Undo = () => RasterCanvas.Children.Remove(img),
            Redo = () => { if (!RasterCanvas.Children.Contains(img)) RasterCanvas.Children.Add(img); }
        });
    }

    // ── 텍스트(EDT-05) ──────────────────────────────────────────────

    private void PlaceTextBox(Point p)
    {
        var tb = new TextBox
        {
            MinWidth = 60,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(_vm.StrokeColor),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 122, 204)),
            BorderThickness = new Thickness(1),
            FontSize = Math.Max(12, _vm.Thickness * 4),
            FontWeight = FontWeights.SemiBold,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Tag = "edit"   // 편집 중 표시(평탄화 시 테두리 제거)
        };
        Canvas.SetLeft(tb, p.X);
        Canvas.SetTop(tb, p.Y);
        OverlayCanvas.Children.Add(tb);
        tb.Focus();

        // 확정: 포커스 잃거나 Esc → 빈 텍스트는 폐기, 아니면 Undo 등록
        void Commit()
        {
            tb.LostKeyboardFocus -= OnLost;
            tb.PreviewKeyDown -= OnKey;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                OverlayCanvas.Children.Remove(tb);
                return;
            }
            // 확정 후엔 테두리 제거(읽기 표시처럼)
            tb.BorderThickness = new Thickness(0);
            tb.IsReadOnly = true;
            tb.Tag = null;
            PushChildAdd(tb, "텍스트", alreadyAdded: true);
        }
        void OnLost(object? s, KeyboardFocusChangedEventArgs e) => Commit();
        void OnKey(object? s, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; Keyboard.ClearFocus(); Commit(); }
        }
        tb.LostKeyboardFocus += OnLost;
        tb.PreviewKeyDown += OnKey;
    }

    // ── 번호 스탬프(EDT-06) ─────────────────────────────────────────

    private void PlaceNumberStamp(Point p)
    {
        int n = _vm.NextNumber;
        double d = Math.Max(22, _vm.Thickness * 6);    // 뱃지 지름

        var badge = new Grid { Width = d, Height = d };
        var circle = new Ellipse
        {
            Fill = new SolidColorBrush(_vm.StrokeColor),
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        var label = new TextBlock
        {
            Text = n.ToString(CultureInfo.InvariantCulture),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = d * 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Children.Add(circle);
        badge.Children.Add(label);
        Canvas.SetLeft(badge, p.X - d / 2);
        Canvas.SetTop(badge, p.Y - d / 2);
        OverlayCanvas.Children.Add(badge);

        _vm.NextNumber = n + 1;
        _vm.Undo.Push(new EditAction
        {
            Label = "번호",
            Undo = () => { OverlayCanvas.Children.Remove(badge); _vm.NextNumber = n; },
            Redo = () => { if (!OverlayCanvas.Children.Contains(badge)) OverlayCanvas.Children.Add(badge); _vm.NextNumber = n + 1; }
        });
    }

    /// <summary>오버레이 자식 추가를 Undo 스택에 등록(이미 추가된 경우 alreadyAdded=true).</summary>
    private void PushChildAdd(UIElement child, string label, bool alreadyAdded = false)
    {
        if (!alreadyAdded && !OverlayCanvas.Children.Contains(child))
            OverlayCanvas.Children.Add(child);

        _vm.Undo.Push(new EditAction
        {
            Label = label,
            Undo = () => OverlayCanvas.Children.Remove(child),
            Redo = () => { if (!OverlayCanvas.Children.Contains(child)) OverlayCanvas.Children.Add(child); }
        });
    }

    // ── 자르기(EDT-08) ──────────────────────────────────────────────
    // 크롭은 즉시 픽셀을 자르지 않고, 확정 시 베이스+오버레이를 현재 상태로 평탄화→그 결과를 새 베이스로 교체하고
    // 캔버스 크기를 자른 크기로 갱신한다(이후 합성·미리보기 크기 일관 — EDT-08 수용기준).

    private void ShowCropOverlay(Point start)
    {
        RemoveCropOverlay();
        _cropRect = new Rectangle
        {
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0))
        };
        Canvas.SetLeft(_cropRect, start.X);
        Canvas.SetTop(_cropRect, start.Y);
        OverlayCanvas.Children.Add(_cropRect);
    }

    private void UpdateCropOverlay(Point start, Point end)
    {
        if (_cropRect is null) return;
        PositionRectLike(_cropRect, start, end);
    }

    private void RemoveCropOverlay()
    {
        if (_cropRect is not null) { OverlayCanvas.Children.Remove(_cropRect); _cropRect = null; }
    }

    private void CommitCrop(Point start, Point end)
    {
        RemoveCropOverlay();
        double x = Math.Min(start.X, end.X), y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(start.X - end.X), h = Math.Abs(start.Y - end.Y);
        if (w < 4 || h < 4) return;

        // 현재 전체를 평탄화한 뒤 선택 영역을 잘라 새 베이스로.
        var full = FlattenToBitmap();
        var rect = new Int32Rect(
            (int)Math.Round(x), (int)Math.Round(y),
            (int)Math.Min(Math.Round(w), full.PixelWidth - Math.Round(x)),
            (int)Math.Min(Math.Round(h), full.PixelHeight - Math.Round(y)));
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var cropped = new CroppedBitmap(full, rect);
        cropped.Freeze();

        // 되돌리기용 이전 상태 보존. 크롭은 평탄화 기반 파괴적 동작이라(이미 그린 주석이 합성본에 구워짐)
        // Undo는 '이전 베이스 + 그 시점 주석들' 복원으로 지원.
        var prevBase = (BitmapSource)BaseImageView.Source;
        double prevW = _pixelW, prevH = _pixelH;
        var prevStrokes = new StrokeCollection(Ink.Strokes);
        var prevOverlay = OverlayCanvas.Children.Cast<UIElement>().ToList();
        var prevRaster = RasterCanvas.Children.Cast<UIElement>().ToList();

        void ApplyCrop()
        {
            BaseImageView.Source = cropped;
            _pixelW = cropped.PixelWidth;
            _pixelH = cropped.PixelHeight;
            Ink.Strokes.Clear();
            OverlayCanvas.Children.Clear();
            RasterCanvas.Children.Clear();
            ApplyCanvasSize();
        }
        void RevertCrop()
        {
            BaseImageView.Source = prevBase;
            _pixelW = prevW; _pixelH = prevH;
            Ink.Strokes.Clear();
            Ink.Strokes.Add(prevStrokes);
            OverlayCanvas.Children.Clear();
            foreach (var c in prevOverlay) OverlayCanvas.Children.Add(c);
            RasterCanvas.Children.Clear();
            foreach (var c in prevRaster) RasterCanvas.Children.Add(c);
            ApplyCanvasSize();
        }

        ApplyCrop();
        _vm.Undo.Push(new EditAction { Label = "자르기", Undo = RevertCrop, Redo = ApplyCrop });
    }

    private void CancelDrag()
    {
        if (_previewShape is not null) { OverlayCanvas.Children.Remove(_previewShape); _previewShape = null; }
        if (_previewArrow is not null) { OverlayCanvas.Children.Remove(_previewArrow); _previewArrow = null; }
        _dragging = false;
        if (OverlayCanvas.IsMouseCaptured) OverlayCanvas.ReleaseMouseCapture();
    }

    // ── 평탄화(§4.0 평탄화 계약) ────────────────────────────────────

    /// <summary>
    /// 베이스 이미지 + 모든 주석(Ink·도형·텍스트·번호·모자이크)을 <b>원본 픽셀 해상도</b>로 합성한다.
    /// CanvasHost(DIP=px)를 RenderTargetBitmap(픽셀크기, 96dpi, Pbgra32)로 렌더 → Freeze 후 반환.
    /// 자르기 후에는 _pixelW/_pixelH가 자른 크기이므로 합성 크기도 자동으로 그에 맞춰진다(EDT-08).
    /// </summary>
    public BitmapSource FlattenToBitmap()
    {
        int pw = Math.Max(1, (int)Math.Round(_pixelW));
        int ph = Math.Max(1, (int)Math.Round(_pixelH));

        // 레이아웃 확정(드물게 Measure/Arrange 미완 상태 방어)
        CanvasHost.Measure(new Size(_pixelW, _pixelH));
        CanvasHost.Arrange(new Rect(0, 0, _pixelW, _pixelH));
        CanvasHost.UpdateLayout();

        var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(CanvasHost);
        rtb.Freeze();
        return rtb;
    }

    // ── 키보드 단축키(§3.3 — 편집기 창 범위) ────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 텍스트 입력 중에는 단일키 단축키 비활성(타이핑 우선 — EDT-05/11)
        bool typing = Keyboard.FocusedElement is TextBox tbx && (tbx.Tag as string) == "edit";

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // Ctrl 조합(타이핑 중에도 허용 — 복사/저장/undo)
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z: if (shift) _vm.Undo.Redo(); else _vm.Undo.Undo(); e.Handled = true; return;
                case Key.Y: _vm.Undo.Redo(); e.Handled = true; return;
                case Key.C: _vm.CopyCommand.Execute(null); e.Handled = true; return;
                case Key.S: _vm.SaveCommand.Execute(null); e.Handled = true; return;
                case Key.W: _vm.CancelCommand.Execute(null); e.Handled = true; return;
                case Key.Enter: _vm.ApplyCommand.Execute(null); e.Handled = true; return;
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            // Esc: 창을 닫지 않고 선택 해제/도구 취소(EDT-11). 진행 중 드래그/크롭만 취소.
            CancelDrag();
            RemoveCropOverlay();
            e.Handled = true;
            return;
        }

        if (typing) return;     // 이하 단일키는 타이핑 중 비활성

        if (e.Key is Key.Delete or Key.Back)
        {
            // 선택 삭제 대용: 직전 동작 되돌리기(개별 선택은 v1 범위 밖, Undo로 대체)
            _vm.Undo.Undo();
            e.Handled = true;
            return;
        }

        // 도구 단일키(P·H·R·O·L·A·T·N·M·C·V)
        switch (e.Key)
        {
            case Key.V: _vm.Tool = EditorTool.Select; break;
            case Key.P: _vm.Tool = EditorTool.Pen; break;
            case Key.H: _vm.Tool = EditorTool.Highlighter; break;
            case Key.R: _vm.Tool = EditorTool.Rectangle; break;
            case Key.O: _vm.Tool = EditorTool.Ellipse; break;
            case Key.L: _vm.Tool = EditorTool.Line; break;
            case Key.A: _vm.Tool = EditorTool.Arrow; break;
            case Key.T: _vm.Tool = EditorTool.Text; break;
            case Key.N: _vm.Tool = EditorTool.Number; break;
            case Key.M: _vm.Tool = EditorTool.Mosaic; break;
            case Key.C: _vm.Tool = EditorTool.Crop; break;
            case Key.OemOpenBrackets:  _vm.Thickness = Math.Max(1, _vm.Thickness - 1); break;   // [
            case Key.OemCloseBrackets: _vm.Thickness = Math.Min(40, _vm.Thickness + 1); break;  // ]
            default: return;
        }
        e.Handled = true;
    }
}
