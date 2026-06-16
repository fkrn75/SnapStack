using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 캡쳐 히스토리 구현 — 캡쳐를 순서대로 적재하고 Sequence를 단일 부여(SSOT). §4.1.
/// </summary>
public sealed class HistoryService : IHistoryService
{
    private readonly ObservableCollection<CaptureItem> _items = new();
    private int _counter;                 // Sequence 부여 카운터(Add 순서)
    private CaptureItem? _selected;

    public ReadOnlyObservableCollection<CaptureItem> Items { get; }

    public HistoryService()
    {
        Items = new ReadOnlyObservableCollection<CaptureItem>(_items);
    }

    public CaptureItem? Selected
    {
        get => _selected;
        set { _selected = value; SelectionChanged?.Invoke(value); }
    }

    public event Action<CaptureItem?>? SelectionChanged;

    public CaptureItem Add(BitmapSource original, Size pixelSize, DateTime capturedAt, CaptureMode mode)
    {
        // 입력 BitmapSource는 Frozen 전제(§4.0 적재 계약).
        var item = new CaptureItem
        {
            Sequence = ++_counter,        // Add 호출 순서로 부여(SSOT) — HIS-01
            CapturedAt = capturedAt,
            Mode = mode,
            Original = original,
            PixelSize = pixelSize,
            Thumbnail = CreateThumbnail(original),
        };
        // 캡쳐 완료 콜백이 백그라운드일 수 있어 Dispatcher 마샬링.
        RunOnUI(() =>
        {
            _items.Add(item);
            Selected = item;              // 새 캡쳐를 선택 상태로
        });
        return item;
    }

    public void Remove(CaptureItem item)
    {
        RunOnUI(() =>
        {
            _items.Remove(item);
            TryDeleteTemp(item);
            if (ReferenceEquals(_selected, item))
                Selected = _items.Count > 0 ? _items[^1] : null;
        });
    }

    public void Clear()
    {
        RunOnUI(() =>
        {
            foreach (var it in _items) TryDeleteTemp(it);
            _items.Clear();
            Selected = null;
        });
    }

    public void MarkEdited(CaptureItem item, BitmapSource composite)
    {
        // editor가 평탄화한 합성본을 받아 EffectiveImage가 합성본을 가리키게 한다(§4.0).
        item.EditedComposite = composite;
        item.IsEdited = true;
        RunOnUI(() =>
        {
            item.Thumbnail = CreateThumbnail(composite);
            if (ReferenceEquals(_selected, item))
                SelectionChanged?.Invoke(item); // 미리보기/썸네일 갱신 알림
        });
    }

    /// <summary>긴 변 256px 썸네일(Frozen). HIS-06.</summary>
    private static BitmapSource CreateThumbnail(BitmapSource src)
    {
        const double max = 256.0;
        double scale = Math.Min(max / src.PixelWidth, max / src.PixelHeight);
        if (scale >= 1.0)
        {
            if (src.CanFreeze && !src.IsFrozen) src.Freeze();
            return src;
        }
        var tb = new TransformedBitmap(src, new ScaleTransform(scale, scale));
        tb.Freeze();
        return tb;
    }

    private static void TryDeleteTemp(CaptureItem item)
    {
        try
        {
            if (item.TempFilePath is { } p && File.Exists(p)) File.Delete(p);
        }
        catch { /* 임시파일 정리 실패는 무시 */ }
    }

    /// <summary>UI 스레드에서 실행(컬렉션은 UI 스레드 소유).</summary>
    private static void RunOnUI(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp is null || disp.CheckAccess()) action();
        else disp.Invoke(action);
    }
}
