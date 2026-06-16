using System.Runtime.InteropServices;

namespace SnapStack.Interop;

/// <summary>
/// 가상화면/모니터 물리 픽셀 좌표(PerMonitorV2 프로세스 기준). §CAP-06.
/// 프로세스가 Per-Monitor V2라 GetSystemMetrics는 물리 픽셀을 반환한다.
/// </summary>
internal static class ScreenMetrics
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77,
                      SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    /// <summary>전체 가상화면(모든 모니터)의 물리 영역.</summary>
    public static (int X, int Y, int W, int H) VirtualScreen() =>
        (GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
         GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));

    /// <summary>주 모니터의 물리 영역(MVP — 커서 위치 모니터 인식은 향후).</summary>
    public static (int X, int Y, int W, int H) PrimaryScreen() =>
        (0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
}
