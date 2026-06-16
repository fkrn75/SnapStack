using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SnapStack.Services;
using SnapStack.ViewModels;

namespace SnapStack;

/// <summary>
/// 앱 진입점 — DI 컨테이너를 구성하고 메인 윈도우를 시작한다.
/// </summary>
public partial class App : Application
{
    /// <summary>전역 서비스 프로바이더.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var main = Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 서비스 (각 영역 구현)
        services.AddSingleton<ISettingsService, SettingsService>();      // §5 설정
        services.AddSingleton<IHistoryService, HistoryService>();        // §4.1 히스토리(순서 SSOT)
        services.AddSingleton<IClipboardService, ClipboardService>();    // §4.2 클립보드
        services.AddSingleton<IFileExportService, FileExportService>();  // §4.4 저장
        services.AddSingleton<ICaptureService, CaptureService>();        // §2 캡쳐

        // 뷰모델 / 윈도우
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
