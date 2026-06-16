using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace SnapStack;

/// <summary>
/// 앱 진입점 — DI 컨테이너를 구성하고 메인 윈도우를 시작한다.
/// 각 영역(캡쳐/히스토리/클립보드/편집/시스템) 서비스는 ConfigureServices에 등록한다.
/// </summary>
public partial class App : Application
{
    /// <summary>전역 서비스 프로바이더(전 영역에서 서비스 해석에 사용).</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // DI 컨테이너 구성
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 메인 윈도우 표시
        var main = Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    /// <summary>
    /// 서비스 등록 지점. 영역 구현 시 각 서비스를 여기에 추가한다.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 윈도우 / 뷰모델
        services.AddSingleton<MainWindow>();

        // TODO(영역 구현 시 등록):
        //   services.AddSingleton<ICaptureService, CaptureService>();   // §2 캡쳐
        //   services.AddSingleton<IHistoryService, HistoryService>();   // §4.1 히스토리(순서 SSOT)
        //   services.AddSingleton<IClipboardService, ClipboardService>(); // §4.2 클립보드
        //   services.AddSingleton<IFileExportService, FileExportService>(); // §4.4 저장
        //   services.AddSingleton<IHotkeyService, HotkeyService>();     // §5 전역 단축키
        //   services.AddSingleton<ISettingsService, SettingsService>(); // §5 설정
    }
}
