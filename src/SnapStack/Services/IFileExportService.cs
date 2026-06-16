using System.Windows.Media.Imaging;
using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 파일 저장/내보내기(§4.4). 평탄화 계약에 따라 EffectiveImage(합성된 비트맵)를 인코딩만 한다.
/// </summary>
public interface IFileExportService
{
    /// <summary>지정 경로·포맷으로 저장. JPG/BMP는 알파를 흰색 매트로 합성. SAV-02.</summary>
    bool Save(BitmapSource image, string path, ImageFormatKind format);

    /// <summary>기본 폴더 + 파일명 규칙으로 즉시 저장하고 생성 경로 반환. SAV-01/03.</summary>
    string QuickSave(BitmapSource image);
}
