using System.Windows;
using SnapStack.ViewModels;

namespace SnapStack.Views;

/// <summary>
/// 설정 윈도우(§5 SYS-04). <see cref="SettingsViewModel"/>을 DI로 주입받아 DataContext에 설정한다.
/// 저장 완료(Saved) 시 창을 닫는다.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 저장 완료 → 창 닫기. ShowDialog로 띄운 경우 DialogResult=true.
        vm.Saved += OnSaved;
        Closed += (_, _) => vm.Saved -= OnSaved;
    }

    private void OnSaved()
    {
        // ShowDialog로 표시된 경우에만 DialogResult 설정이 유효하므로 예외를 흡수한다.
        try
        {
            DialogResult = true;
        }
        catch (System.InvalidOperationException)
        {
            // 모달이 아닌 경우(Show) — DialogResult 설정 불가, 단순 Close로 처리.
            Close();
        }
    }
}
