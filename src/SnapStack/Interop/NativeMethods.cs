using System.Runtime.InteropServices;

namespace SnapStack.Interop;

/// <summary>Win32 P/Invoke 래퍼 모음.</summary>
internal static class NativeMethods
{
    /// <summary>GDI 객체 해제(GetHbitmap 등으로 만든 HBITMAP 누수 방지).</summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    // ───────── 창 캡쳐(CAP-02)용 ─────────

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>커서의 화면 좌표(PerMonitorV2라 물리 픽셀).</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>최상위 창을 Z순서(상위→하위)로 열거.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>바탕화면 셸 창(Progman) 핸들 — 창 선택에서 제외용.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    /// <summary>PrintWindow 플래그: DirectComposition 포함 전체 콘텐츠 렌더(Win8.1+).</summary>
    public const int PW_RENDERFULLCONTENT = 2;

    /// <summary>창의 내용을 DC로 직접 렌더(가려져 있어도 캡쳐 가능).</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

    // DWM 속성
    public const int DWMWA_CLOAKED = 14;               // 다른 가상데스크탑 등으로 가려진 창
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;  // 드롭섀도 제외 실제 가시 경계

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
}
