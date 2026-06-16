using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 화면 캡쳐(§2). 각 모드는 성공 시 히스토리에 적재된 <see cref="CaptureItem"/>을 반환(취소 시 null).
/// 캡쳐는 단일 진행 가드를 지킨다(동시 캡쳐 금지).
/// </summary>
public interface ICaptureService
{
    /// <summary>영역 지정 캡쳐 — 오버레이로 사각 영역 선택. CAP-01.</summary>
    Task<CaptureItem?> CaptureRegionAsync();

    /// <summary>활성 창/단위 영역 캡쳐 — 마우스 아래 창 자동 인식. CAP-02.</summary>
    Task<CaptureItem?> CaptureWindowAsync();

    /// <summary>전체 화면 캡쳐. virtualFull=true면 전체 가상화면(모든 모니터). CAP-03.</summary>
    CaptureItem CaptureFullScreen(bool virtualFull = false);

    /// <summary>직전 영역 재캡쳐(없으면 null). CAP-04.</summary>
    CaptureItem? CaptureLastRegion();

    /// <summary>지연 캡쳐 — seconds 카운트다운 후 mode로 캡쳐. CAP-05.</summary>
    Task<CaptureItem?> CaptureDelayedAsync(int seconds, CaptureMode mode);
}
