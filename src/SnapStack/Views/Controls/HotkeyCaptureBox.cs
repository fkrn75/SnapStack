using System;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SnapStack.Views.Controls;

/// <summary>
/// 전역 단축키 재지정용 입력 컨트롤(§5 SYS-04 단축키 재지정).
/// 포커스 상태에서 사용자가 누른 키 조합을 가로채 "Ctrl+Shift+W" 형태의 제스처 문자열로 표시·노출한다.
///
/// <para>설계:</para>
/// <list type="bullet">
///   <item>읽기 전용 <see cref="System.Windows.Controls.TextBox"/> 파생. 직접 타이핑은 막고 키 조합만 캡쳐한다.</item>
///   <item><see cref="Hotkey"/> DependencyProperty(<see cref="string"/>, 양방향)로 값을 노출 →
///         현행 <c>AppSettings.Hotkeys</c>(동작명 → 제스처 문자열) 저장형식과 그대로 호환.</item>
///   <item>출력 토큰은 <c>HotkeyService.TryParseGesture</c>가 받는 토큰과 정확히 일치하도록 생성한다
///         (수정자 "Ctrl"/"Shift"/"Alt"/"Win" + 본문 키는 WPF <see cref="KeyConverter"/> 역변환 문자열).
///         즉 여기서 만든 문자열을 파서가 다시 읽으면 왕복(round-trip) 일치한다.</item>
///   <item>순수 수정자(Ctrl/Shift/Alt/Win)만 눌린 상태는 무시(본문 키가 와야 확정).</item>
///   <item>Esc / Backspace / Delete = 값 비우기(빈 문자열).</item>
/// </list>
/// </summary>
public sealed class HotkeyCaptureBox : System.Windows.Controls.TextBox
{
    /// <summary>본문 키가 아직 안 눌린 '비어있지 않은' 상태에서 보여줄 안내 텍스트.</summary>
    private const string PlaceholderText = "키 조합을 누르세요…";

    /// <summary>
    /// 캡쳐된 단축키 제스처 문자열(예: "Ctrl+Shift+W"). 빈 문자열이면 미지정.
    /// 현행 <c>AppSettings.Hotkeys</c> 값 형식과 동일하므로 바인딩만으로 저장형식과 호환된다.
    /// </summary>
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    /// <summary>캡쳐된 제스처 문자열. <see cref="HotkeyProperty"/> 참조.</summary>
    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>
    /// 단축키 값이 바뀔 때 발생(사용자 입력·외부 바인딩 모두). 인자는 새 제스처 문자열(빈 문자열 가능).
    /// 뷰모델이 중복 검증 등 즉시 반응이 필요하면 구독한다(바인딩으로 충분하면 무시 가능).
    /// </summary>
    public event EventHandler<string>? HotkeyChanged;

    public HotkeyCaptureBox()
    {
        // 직접 타이핑 방지: 표시 전용이되 포커스는 받아 키 이벤트를 가로챈다.
        IsReadOnly = true;
        // 캐럿/우클릭 컨텍스트 메뉴(붙여넣기 등)로 값이 바뀌는 경로 차단.
        IsReadOnlyCaretVisible = false;
        ContextMenu = null;
        Cursor = System.Windows.Input.Cursors.Hand;

        // IME가 키를 가로채면 제스처 캡쳐가 깨지므로 비활성화.
        InputMethod.SetIsInputMethodEnabled(this, false);

        UpdateDisplay();
    }

    /// <summary>외부에서 Hotkey가 바뀌면 표시를 갱신하고 변경 이벤트를 발생시킨다.</summary>
    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HotkeyCaptureBox box) return;
        box.UpdateDisplay();
        box.HotkeyChanged?.Invoke(box, (string)(e.NewValue ?? string.Empty));
    }

    /// <summary>표시 텍스트를 현재 <see cref="Hotkey"/> 값으로 동기화. 비어 있으면 안내문을 보여준다.</summary>
    private void UpdateDisplay()
    {
        // TextBox.Text를 직접 세팅(IsReadOnly여도 코드에서는 설정 가능).
        Text = string.IsNullOrEmpty(Hotkey) ? PlaceholderText : Hotkey;
        // 표시만 갱신하고 캐럿은 끝으로.
        CaretIndex = Text.Length;
    }

    /// <summary>
    /// 포커스 상태의 키 입력을 가로채 제스처로 변환한다.
    /// 시스템 키(Alt 조합)도 받기 위해 KeyDown이 아닌 PreviewKeyDown을 오버라이드한다.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // 모든 키 입력을 이 컨트롤이 소비(상위로 전파해 메뉴/단축키가 발동하지 않도록).
        e.Handled = true;

        // Alt/F10 등은 SystemKey로 들어오므로 보정.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 비우기 키: Esc / Backspace / Delete
        if (key is Key.Escape or Key.Back or Key.Delete)
        {
            SetHotkey(string.Empty);
            return;
        }

        // 순수 수정자 키만 눌린 상태(아직 본문 키 없음)는 확정하지 않는다.
        if (IsModifierKey(key))
            return;

        ModifierKeys mods = Keyboard.Modifiers;

        // 본문 키를 토큰 문자열로 변환할 수 없으면(매핑 불가 키) 무시.
        if (!TryGetKeyToken(key, out string keyToken))
            return;

        string gesture = BuildGesture(mods, keyToken);
        if (!string.IsNullOrEmpty(gesture))
            SetHotkey(gesture);
    }

    /// <summary>마우스로 누르면 즉시 포커스를 받아 바로 캡쳐 가능하게 한다.</summary>
    protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            Focus();
            e.Handled = true;
        }
        base.OnPreviewMouseDown(e);
    }

    /// <summary>값 설정 + 표시 갱신. 동일 값이면 표시만 갱신.</summary>
    private void SetHotkey(string gesture)
    {
        if (string.Equals(Hotkey, gesture, StringComparison.Ordinal))
        {
            UpdateDisplay();
            return;
        }
        // OnHotkeyChanged 콜백에서 UpdateDisplay + HotkeyChanged 발생.
        Hotkey = gesture;
    }

    /// <summary>해당 키가 수정자(Ctrl/Shift/Alt/Win) 자체인지.</summary>
    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.System or              // Alt 단독은 종종 System으로 들어옴
        Key.LWin or Key.RWin;

    /// <summary>
    /// 수정자 + 본문 키 토큰을 "Ctrl+Shift+W" 제스처로 조립.
    /// 토큰 표기는 <c>HotkeyService.TryParseGesture</c>가 인식하는 "Ctrl"/"Shift"/"Alt"/"Win"과 일치시킨다.
    /// 순서: Ctrl → Shift → Alt → Win → 본문 키.
    /// </summary>
    private static string BuildGesture(ModifierKeys mods, string keyToken)
    {
        var sb = new StringBuilder();

        if (mods.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
        if (mods.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
        if (mods.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
        if (mods.HasFlag(ModifierKeys.Windows)) sb.Append("Win+");

        sb.Append(keyToken);
        return sb.ToString();
    }

    /// <summary>
    /// 본문 키(Key)를 제스처 토큰 문자열로 변환.
    /// 파서가 <see cref="KeyConverter"/>로 다시 읽으므로, 역변환 문자열을 그대로 토큰으로 쓴다(왕복 일치).
    /// 변환 불가/공백이면 false → 캡쳐 무시.
    /// </summary>
    private static bool TryGetKeyToken(Key key, out string token)
    {
        token = string.Empty;
        if (key == Key.None) return false;

        // WPF KeyConverter 역변환: A→"A", D5→"D5", F5→"F5", Enter→"Return" 등.
        // 파서(TryParseKey)가 동일 KeyConverter.ConvertFromInvariantString으로 받으므로 round-trip 보장.
        try
        {
            var converter = new KeyConverter();
            string? s = converter.ConvertToInvariantString(key);
            if (!string.IsNullOrWhiteSpace(s))
            {
                token = s;
                return true;
            }
        }
        catch
        {
            // 변환 실패는 아래 폴백 없이 무시(미지원 키).
        }

        return false;
    }
}
