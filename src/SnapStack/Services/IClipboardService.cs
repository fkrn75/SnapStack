using System.Windows.Media.Imaging;

namespace SnapStack.Services;

/// <summary>
/// 클립보드 복사/받기(필수 기능 #3·#4, §4.2). 외부 앱 붙여넣기 호환성의 핵심:
/// 하나의 DataObject에 <b>PNG + CF_DIBV5 + CF_DIB</b> 다중 포맷을 동시 등록한다(PST-01).
/// </summary>
public interface IClipboardService
{
    /// <summary>이미지를 다중 포맷으로 클립보드에 복사. 성공/실패 반환(토스트는 호출 측). CLP-01.</summary>
    bool CopyImage(BitmapSource image);

    /// <summary>클립보드에서 이미지 받기(PNG 우선 → DIBV5 → DIB). 없으면 null. CLP-02.</summary>
    BitmapSource? TryGetImage();
}
