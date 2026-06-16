using System;
using System.Windows;

namespace SnapStack.Services;

/// <summary>
/// 시스템 트레이 상주 서비스(§5 SYS-02). 트레이 아이콘 + 컨텍스트 메뉴 + 더블클릭 동작.
/// 트레이는 ViewModel을 직접 알지 않고, 사용자가 메뉴/더블클릭으로 요청한 동작을
/// <see cref="CommandRequested"/> 이벤트의 action 문자열로만 전달한다.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>메인 윈도우를 받아 트레이 아이콘을 생성·표시한다. 더블클릭=메인창 표시 처리.</summary>
    void Initialize(Window mainWindow);

    /// <summary>
    /// 트레이 메뉴/더블클릭이 요청한 동작.
    /// action: "ShowMain" / "CaptureRegion" / "CaptureFullScreen" / "Exit".
    /// </summary>
    event Action<string>? CommandRequested;
}
