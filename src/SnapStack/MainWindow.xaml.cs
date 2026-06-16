using System.Windows;
using SnapStack.ViewModels;

namespace SnapStack;

/// <summary>
/// 메인 윈도우 — 캡쳐 도구바 + 히스토리 스트립 + 미리보기.
/// ViewModel을 DI로 주입받아 DataContext에 설정한다.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
