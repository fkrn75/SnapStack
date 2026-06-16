using System;

namespace SnapStack.Services;

/// <summary>
/// 전역 단축키 서비스(§5 SYS-01). Win32 RegisterHotKey + WM_HOTKEY 후킹.
/// <see cref="Models.AppSettings.Hotkeys"/>(동작명 → 제스처 문자열)를 파싱해 등록하고,
/// 핫키가 눌리면 동작명을 emit한다. 등록 실패(타 앱 선점)한 키는 건너뛴다.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// 메인 윈도우 핸들로 초기화. HwndSource에 메시지 훅을 걸고 설정의 모든 핫키를 등록한다.
    /// 등록 실패한 핫키(이미 점유된 조합)는 건너뛰고 계속한다.
    /// </summary>
    void Initialize(IntPtr hwnd);

    /// <summary>핫키가 눌렸을 때 동작명(예: "CaptureRegion")을 전달한다.</summary>
    event Action<string>? HotkeyPressed;

    /// <summary>
    /// 설정 변경 후 핫키를 재적용한다. 기존 등록을 모두 해제하고 settings의 핫키를 다시 등록한다.
    /// Initialize 호출 전이면 아무 일도 하지 않는다.
    /// </summary>
    /// <returns>등록에 실패한(타 앱 선점 등) 동작명 목록. 전부 성공이면 빈 목록.</returns>
    System.Collections.Generic.IReadOnlyList<string> Reapply(Models.AppSettings settings);
}
