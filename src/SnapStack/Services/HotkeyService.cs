using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SnapStack.Services;

/// <summary>
/// 전역 단축키 구현(§5 SYS-01). Win32 <c>RegisterHotKey</c>/<c>UnregisterHotKey</c> +
/// <c>WM_HOTKEY</c>를 <see cref="HwndSource.AddHook"/>로 수신한다.
/// <see cref="Models.AppSettings.Hotkeys"/>(동작명 → "Ctrl+Shift+A" 제스처)를 MOD+VK로 파싱해 등록하며,
/// 이미 점유된 조합 등으로 등록 실패한 핫키는 건너뛰고 나머지를 계속 등록한다.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;

    // RegisterHotKey 수정자 플래그
    [Flags]
    private enum HotkeyModifiers : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000, // 키 반복 억제(Win7+)
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly ISettingsService _settings;

    // 등록한 핫키 ID → 동작명. WM_HOTKEY 수신 시 역참조한다.
    private readonly Dictionary<int, string> _actionsById = new();

    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private int _nextId = 1;            // RegisterHotKey 식별자(앱 내 고유)
    private bool _disposed;

    public event Action<string>? HotkeyPressed;

    public HotkeyService(ISettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize(IntPtr hwnd)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyService));
        if (hwnd == IntPtr.Zero) throw new ArgumentException("유효한 윈도우 핸들이 필요합니다.", nameof(hwnd));
        if (_hwnd != IntPtr.Zero) return; // 중복 초기화 방지

        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        RegisterAll();
    }

    /// <summary>설정의 모든 핫키를 등록. 파싱/등록 실패 키는 건너뛴다.</summary>
    private void RegisterAll()
    {
        foreach (var (action, gesture) in _settings.Current.Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(gesture)) continue;
            if (!TryParseGesture(gesture, out var mods, out var vk)) continue; // 파싱 실패 → 건너뜀

            int id = _nextId++;
            // NoRepeat로 키 홀드 시 연속 발동 방지
            if (RegisterHotKey(_hwnd, id, (uint)(mods | HotkeyModifiers.NoRepeat), vk))
                _actionsById[id] = action;
            // 실패(타 앱 선점 등)는 의도적으로 무시하고 다음 키로 진행(SYS-01).
        }
    }

    /// <summary>
    /// "Ctrl+Shift+A" 형태의 제스처를 Win32 수정자 플래그 + 가상키 코드로 파싱한다.
    /// 지원 토큰: Ctrl/Control, Shift, Alt, Win/Windows + 단일 키(A-Z, 0-9, F1-F24, 일부 특수키).
    /// </summary>
    private static bool TryParseGesture(string gesture, out HotkeyModifiers mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        bool hasKey = false;

        foreach (var raw in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= HotkeyModifiers.Control;
                    break;
                case "shift":
                    mods |= HotkeyModifiers.Shift;
                    break;
                case "alt":
                    mods |= HotkeyModifiers.Alt;
                    break;
                case "win":
                case "windows":
                    mods |= HotkeyModifiers.Win;
                    break;
                default:
                    // 본문 키는 하나만 허용
                    if (hasKey) return false;
                    if (!TryParseKey(raw, out vk)) return false;
                    hasKey = true;
                    break;
            }
        }

        return hasKey && vk != 0;
    }

    /// <summary>본문 키 토큰을 가상키 코드(VK)로 변환. WPF <see cref="Key"/> 파싱을 활용.</summary>
    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;

        // WPF KeyConverter로 "A", "F5", "Enter" 등 폭넓게 파싱
        try
        {
            var converter = new KeyConverter();
            if (converter.ConvertFromInvariantString(token) is Key key && key != Key.None)
            {
                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                if (virtualKey > 0)
                {
                    vk = (uint)virtualKey;
                    return true;
                }
            }
        }
        catch
        {
            // 단일 문자(예: "A") 폴백 처리
        }

        // 폴백: 단일 영문/숫자 문자
        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                vk = c; // VK 코드는 'A'~'Z'/'0'~'9' ASCII와 동일
                return true;
            }
        }

        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_actionsById.TryGetValue(id, out var action))
            {
                HotkeyPressed?.Invoke(action);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            foreach (var id in _actionsById.Keys)
                UnregisterHotKey(_hwnd, id);
        }
        _actionsById.Clear();

        _source?.RemoveHook(WndProc);
        _source = null;
        _hwnd = IntPtr.Zero;
    }
}
