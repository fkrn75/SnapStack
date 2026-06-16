using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapStack.Models;

/// <summary>캡쳐 모드(§2 캡쳐 기능).</summary>
public enum CaptureMode
{
    Region,       // 영역 지정 (CAP-01)
    Window,       // 활성 창/단위 영역 (CAP-02)
    FullScreen,   // 전체 화면(현재 모니터) (CAP-03)
    VirtualFull,  // 전체 가상화면(모든 모니터) (CAP-03)
    LastRegion,   // 직전 영역 재캡쳐 (CAP-04)
    Delayed       // 지연 캡쳐 (CAP-05)
}

/// <summary>
/// 캡쳐 1건 = 히스토리의 한 항목(순서 보존 단위). §4.0 데이터 계약.
/// 모든 BitmapSource는 Frozen 전제. EffectiveImage 단일 규칙으로 복사·저장·미리보기·썸네일이 일관.
/// </summary>
public sealed class CaptureItem
{
    /// <summary>캡쳐가 부여하는 고유 ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>히스토리가 Add 호출 순서로 부여(표시·정렬 SSOT). §4.1 HIS-01.</summary>
    public int Sequence { get; set; }

    public DateTime CapturedAt { get; init; }
    public CaptureMode Mode { get; init; }

    /// <summary>원본 비트맵(Frozen, Pbgra32). 캡쳐가 산출.</summary>
    public required BitmapSource Original { get; init; }

    public Size PixelSize { get; init; }

    /// <summary>편집 여부.</summary>
    public bool IsEdited { get; set; }

    /// <summary>편집 결과 합성본(Frozen, nullable). 편집 시 editor가 FlattenToBitmap()으로 채움. §4.0 평탄화 계약.</summary>
    public BitmapSource? EditedComposite { get; set; }

    // ── 히스토리 내부 관리 필드 ──
    /// <summary>썸네일(긴 변 256px, Frozen). 메모리 상주. HIS-06.</summary>
    public BitmapSource? Thumbnail { get; set; }

    /// <summary>원본/편집본 임시파일 경로(lazy load·영속). HIS-05/06.</summary>
    public string? TempFilePath { get; set; }

    /// <summary>
    /// EffectiveImage 단일 규칙(§4.0): 편집됐고 합성본이 있으면 합성본, 아니면 원본.
    /// 복사·저장·미리보기·썸네일 재생성이 전부 이 속성을 참조한다.
    /// </summary>
    public BitmapSource EffectiveImage =>
        (IsEdited && EditedComposite is not null) ? EditedComposite : Original;
}
