using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace SnapStack.Services;

/// <summary>
/// 시스템 트레이 구현(§5 SYS-02). 외부 패키지 없이 WinForms <see cref="WinForms.NotifyIcon"/> 사용
/// (WPF와 병용 — UseWindowsForms). 외부 .ico 없이 System.Drawing으로 텍스트 "S" 아이콘을 생성하고,
/// 컨텍스트 메뉴(열기/영역 캡쳐/전체화면/종료)와 더블클릭(메인창 표시)을 제공한다.
/// 동작은 <see cref="CommandRequested"/> 이벤트로만 외부에 알린다(ViewModel 미참조).
/// </summary>
public sealed class TrayService : ITrayService
{
    private WinForms.NotifyIcon? _icon;
    private Icon? _generatedIcon;     // GetHicon 비관리 핸들 소유 — Dispose에서 정리
    private Window? _mainWindow;
    private bool _disposed;

    public event Action<string>? CommandRequested;

    public void Initialize(Window mainWindow)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TrayService));
        if (_icon is not null) return; // 중복 초기화 방지

        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _generatedIcon = CreateTextIcon("S");

        // 우클릭 컨텍스트 메뉴(열기/영역 캡쳐/전체화면/종료)
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(MakeItem("열기", ShowMain));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(MakeItem("영역 캡쳐", () => Raise("CaptureRegion")));
        menu.Items.Add(MakeItem("전체화면 캡쳐", () => Raise("CaptureFullScreen")));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(MakeItem("종료", () => Raise("Exit")));

        _icon = new WinForms.NotifyIcon
        {
            Text = "SnapStack — 화면 캡쳐",
            Icon = _generatedIcon,
            Visible = true,
            ContextMenuStrip = menu,
        };
        // 더블클릭 = 메인창 표시
        _icon.DoubleClick += (_, _) => ShowMain();
    }

    private static WinForms.ToolStripMenuItem MakeItem(string header, Action onClick)
    {
        var item = new WinForms.ToolStripMenuItem(header);
        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>System.Drawing으로 배경(SteelBlue)+텍스트(흰색 "S") 트레이 아이콘을 생성.</summary>
    private static Icon CreateTextIcon(string text)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.SteelBlue);
            using var font = new Font("Segoe UI", 18, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(text, font, brush, new RectangleF(0, 0, 32, 32), sf);
        }
        // GetHicon은 비관리 아이콘 핸들 — Icon으로 감싸고 Dispose에서 DestroyIcon으로 해제
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>"열기"/더블클릭 공통: 메인창을 복원·표시(UI 스레드 마샬링)하고 ShowMain 동작을 알린다.</summary>
    private void ShowMain()
    {
        if (_mainWindow is { } w)
        {
            w.Dispatcher.Invoke(() =>
            {
                w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
            });
        }
        Raise("ShowMain");
    }

    /// <summary>트레이 풍선 알림(NotifyIcon BalloonTip). 아이콘 미초기화면 무시.</summary>
    public void ShowBalloon(string title, string text)
    {
        _icon?.ShowBalloonTip(3000, title, text, WinForms.ToolTipIcon.Info);
    }

    private void Raise(string action) => CommandRequested?.Invoke(action);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
        if (_generatedIcon is not null)
        {
            var handle = _generatedIcon.Handle;
            _generatedIcon.Dispose();
            DestroyIcon(handle);          // GetHicon 핸들 해제(누수 방지)
            _generatedIcon = null;
        }
        _mainWindow = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
