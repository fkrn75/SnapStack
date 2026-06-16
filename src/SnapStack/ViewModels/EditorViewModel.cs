using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnapStack.Editor;
using SnapStack.Models;
using SnapStack.Services;

namespace SnapStack.ViewModels;

/// <summary>
/// 편집기 뷰모델(§3 EDT). 도구·색·굵기 상태와 툴바 명령(적용/복사/저장/취소·Undo/Redo)을 보유.
/// 실제 캔버스 드로잉과 평탄화는 View(EditorWindow)가 담당하며, VM은 <see cref="Flatten"/>(View가 주입)
/// 으로 합성본을 얻어 서비스(클립보드·파일·히스토리)를 호출한다. §4.0 평탄화/적재 계약 준수.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    private readonly CaptureItem _item;
    private readonly IClipboardService _clipboard;
    private readonly IFileExportService _file;
    private readonly IHistoryService _history;

    /// <summary>편집 대상 캡쳐 항목.</summary>
    public CaptureItem Item => _item;

    /// <summary>베이스로 깔 이미지(EffectiveImage — 이미 편집됐으면 합성본, 아니면 원본). §4.0.</summary>
    public BitmapSource BaseImage => _item.EffectiveImage;

    /// <summary>Undo/Redo 스택(EDT-10). View가 드로잉마다 EditAction을 push.</summary>
    public UndoStack Undo { get; } = new();

    /// <summary>
    /// View가 주입하는 평탄화 델리게이트. 베이스+모든 주석을 원본 픽셀 1:1·Pbgra32·frozen으로 합성.
    /// (§4.0 평탄화 계약. 실제 RenderTargetBitmap 합성은 EditorWindow.FlattenToBitmap.)
    /// </summary>
    public Func<BitmapSource>? Flatten { get; set; }

    /// <summary>창 닫기 요청(결과 DialogResult). View가 구독해 Window.Close.</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>상태/오류 토스트 메시지(하단 상태바).</summary>
    public event Action<string>? Notify;

    // ── 도구·옵션 상태(옵션바 바인딩) ──
    [ObservableProperty] private EditorTool _tool = EditorTool.Pen;
    [ObservableProperty] private Color _strokeColor = Colors.Red;
    [ObservableProperty] private double _thickness = 4;
    [ObservableProperty] private bool _fillEnabled;
    [ObservableProperty] private Color _fillColor = Colors.Transparent;
    [ObservableProperty] private string _status = "편집 — 도구를 선택하세요";

    /// <summary>번호 스탬프 다음 값(EDT-06). 배치마다 증가, Undo 시 되돌림은 View가 관리.</summary>
    public int NextNumber { get; set; } = 1;

    public EditorViewModel(CaptureItem item, IClipboardService clipboard,
                           IFileExportService file, IHistoryService history)
    {
        _item = item;
        _clipboard = clipboard;
        _file = file;
        _history = history;

        // 스택 변경 시 Undo/Redo 버튼 활성 재평가
        Undo.Changed += () =>
        {
            UndoEditCommand.NotifyCanExecuteChanged();
            RedoEditCommand.NotifyCanExecuteChanged();
        };
    }

    // 도구 변경 시: 상태바 안내 + 도구바 ToggleButton 활성 표시 갱신(아래 Is*Tool 속성)
    partial void OnToolChanged(EditorTool value)
    {
        Status = $"도구: {ToolLabel(value)}";
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsPenTool));
        OnPropertyChanged(nameof(IsHighlighterTool));
        OnPropertyChanged(nameof(IsRectangleTool));
        OnPropertyChanged(nameof(IsEllipseTool));
        OnPropertyChanged(nameof(IsLineTool));
        OnPropertyChanged(nameof(IsArrowTool));
        OnPropertyChanged(nameof(IsTextTool));
        OnPropertyChanged(nameof(IsNumberTool));
        OnPropertyChanged(nameof(IsMosaicTool));
        OnPropertyChanged(nameof(IsCropTool));
    }

    // 도구바 ToggleButton 활성 바인딩(활성 도구만 IsChecked)
    public bool IsSelectTool => Tool == EditorTool.Select;
    public bool IsPenTool => Tool == EditorTool.Pen;
    public bool IsHighlighterTool => Tool == EditorTool.Highlighter;
    public bool IsRectangleTool => Tool == EditorTool.Rectangle;
    public bool IsEllipseTool => Tool == EditorTool.Ellipse;
    public bool IsLineTool => Tool == EditorTool.Line;
    public bool IsArrowTool => Tool == EditorTool.Arrow;
    public bool IsTextTool => Tool == EditorTool.Text;
    public bool IsNumberTool => Tool == EditorTool.Number;
    public bool IsMosaicTool => Tool == EditorTool.Mosaic;
    public bool IsCropTool => Tool == EditorTool.Crop;

    private static string ToolLabel(EditorTool t) => t switch
    {
        EditorTool.Select => "선택",
        EditorTool.Pen => "펜",
        EditorTool.Highlighter => "형광펜",
        EditorTool.Rectangle => "사각형",
        EditorTool.Ellipse => "타원",
        EditorTool.Line => "직선",
        EditorTool.Arrow => "화살표",
        EditorTool.Text => "텍스트",
        EditorTool.Number => "번호",
        EditorTool.Mosaic => "모자이크",
        EditorTool.Crop => "자르기",
        _ => t.ToString()
    };

    // ── 도구 선택 명령(좌측 도구바) ──
    [RelayCommand] private void SelectTool() => Tool = EditorTool.Select;
    [RelayCommand] private void Pen() => Tool = EditorTool.Pen;
    [RelayCommand] private void Highlighter() => Tool = EditorTool.Highlighter;
    [RelayCommand] private void RectangleTool() => Tool = EditorTool.Rectangle;
    [RelayCommand] private void EllipseTool() => Tool = EditorTool.Ellipse;
    [RelayCommand] private void LineTool() => Tool = EditorTool.Line;
    [RelayCommand] private void ArrowTool() => Tool = EditorTool.Arrow;
    [RelayCommand] private void TextTool() => Tool = EditorTool.Text;
    [RelayCommand] private void NumberTool() => Tool = EditorTool.Number;
    [RelayCommand] private void MosaicTool() => Tool = EditorTool.Mosaic;
    [RelayCommand] private void CropTool() => Tool = EditorTool.Crop;

    // ── Undo/Redo(EDT-10) ──
    [RelayCommand(CanExecute = nameof(CanUndo))] private void UndoEdit() => Undo.Undo();
    [RelayCommand(CanExecute = nameof(CanRedo))] private void RedoEdit() => Undo.Redo();
    private bool CanUndo() => Undo.CanUndo;
    private bool CanRedo() => Undo.CanRedo;

    /// <summary>View가 주입한 Flatten 호출. 미주입 시 베이스 폴백(방어).</summary>
    private BitmapSource Composite() => Flatten?.Invoke() ?? BaseImage;

    /// <summary>
    /// 적용/확인(EDT-11): 합성본을 히스토리에 통지(MarkEdited) 후 창 닫기(DialogResult=true).
    /// EditedComposite·IsEdited가 채워져 미리보기·복사·저장에 자동 반영(§4.0).
    /// </summary>
    [RelayCommand]
    private void Apply()
    {
        try
        {
            _history.MarkEdited(_item, Composite());
            CloseRequested?.Invoke(true);
        }
        catch
        {
            Notify?.Invoke("적용 실패");
        }
    }

    /// <summary>복사(CLP-01, Ctrl+C): 합성본을 클립보드에 다중 포맷 등록.</summary>
    [RelayCommand]
    private void Copy()
    {
        bool ok = _clipboard.CopyImage(Composite());
        var msg = ok ? "클립보드에 복사됨 — 다른 앱에서 Ctrl+V" : "복사 실패";
        Status = msg;
        Notify?.Invoke(msg);
    }

    /// <summary>빠른 저장(SAV-01, Ctrl+S): 합성본을 기본 폴더·파일명 규칙으로 저장.</summary>
    [RelayCommand]
    private void Save()
    {
        try
        {
            var path = _file.QuickSave(Composite());
            var msg = $"저장됨: {path}";
            Status = msg;
            Notify?.Invoke(msg);
        }
        catch
        {
            Notify?.Invoke("저장 실패");
        }
    }

    /// <summary>취소(EDT-11): 변경 폐기하고 닫기(DialogResult=false). 미저장 확인은 View에서.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
