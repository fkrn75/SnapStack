using SnapStack.Models;

namespace SnapStack.Services;

/// <summary>
/// 전역 단축키(§5 SYS-01). Win32 RegisterHotKey + WM_HOTKEY 후킹.
/// 등록 실패(타 앱 선점)는 해당 키만 비활성하고 알린다.
/// </summary>
public interface IHotkeyService
{
    /// <summary>설정의 단축키 매핑으로 전역 핫키 등록(메인 윈도우 핸들 필요).</summary>
    void Register(IntPtr windowHandle);

    /// <summary>전역 핫키 해제(앱 종료 시).</summary>
    void Unregister();

    /// <summary>핫키 발동 시 해당 캡쳐 모드를 전달.</summary>
    event Action<CaptureMode>? Triggered;
}
