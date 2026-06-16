using System.Windows;

namespace SnapStack;

/// <summary>
/// 메인 윈도우 — 캡쳐 도구바 + 히스토리 썸네일 스트립 + 미리보기.
/// 현재는 M0 뼈대(레이아웃만). 각 영역 구현 시 ViewModel을 DataContext로 주입한다.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
