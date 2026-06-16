using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 캡쳐 히스토리(필수 기능 #1, §4.1). 캡쳐를 <b>순서대로</b> 누적하고 Sequence를 단일 부여(SSOT).
/// </summary>
public interface IHistoryService
{
    /// <summary>표시용 컬렉션(Sequence 오름차순). UI가 바인딩.</summary>
    ReadOnlyObservableCollection<CaptureItem> Items { get; }

    /// <summary>현재 선택 항목(HIS-02). 변경 시 미리보기 갱신.</summary>
    CaptureItem? Selected { get; set; }

    /// <summary>선택 변경 알림.</summary>
    event Action<CaptureItem?>? SelectionChanged;

    /// <summary>
    /// 캡쳐 결과를 적재하고 Sequence를 부여(Add 호출 순서). 내부에서 Dispatcher 마샬링·썸네일 생성. §4.0 적재 계약·HIS-01.
    /// </summary>
    /// <param name="original">Frozen 원본 비트맵.</param>
    CaptureItem Add(BitmapSource original, Size pixelSize, DateTime capturedAt, CaptureMode mode);

    /// <summary>항목 삭제(연결 임시파일 정리). HIS-04.</summary>
    void Remove(CaptureItem item);

    /// <summary>전체 비우기. HIS-04.</summary>
    void Clear();

    /// <summary>편집 결과 통지: EditedComposite 채우고 IsEdited=true, 썸네일 재생성. HIS-03/§4.0.</summary>
    void MarkEdited(CaptureItem item, BitmapSource composite);
}
