using System.Runtime.InteropServices;

namespace SnapStack.Interop;

/// <summary>Win32 P/Invoke 래퍼 모음.</summary>
internal static class NativeMethods
{
    /// <summary>GDI 객체 해제(GetHbitmap 등으로 만든 HBITMAP 누수 방지).</summary>
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);
}
