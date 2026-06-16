using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SnapStack.Services;
using SnapStack.ViewModels;
using SnapStack.Views;

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

        // 트레이 상주: 창을 닫거나 숨겨도 앱이 종료되지 않게 한다(종료는 트레이 '종료'에서 명시적으로만).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var main = Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    // 앱 종료 시 DI 컨테이너를 정리 → 싱글톤 IDisposable(TrayService·HotkeyService 등) 자동 Dispose.
    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 서비스 (각 영역 구현)
        services.AddSingleton<ISettingsService, SettingsService>();      // §5 설정
        services.AddSingleton<IHistoryService, HistoryService>();        // §4.1 히스토리(순서 SSOT)
        services.AddSingleton<IClipboardService, ClipboardService>();    // §4.2 클립보드
        services.AddSingleton<IFileExportService, FileExportService>();  // §4.4 저장
        services.AddSingleton<ICaptureService, CaptureService>();        // §2 캡쳐
        services.AddSingleton<IHotkeyService, HotkeyService>();          // §5 전역 단축키(SYS-01)
        services.AddSingleton<ITrayService, TrayService>();             // §5 시스템 트레이(SYS-02)

        // 뷰모델 / 윈도우
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();                     // §5 설정창(SYS-04)
        services.AddTransient<SettingsWindow>();
    }
}
