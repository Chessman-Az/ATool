using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ATool.Data;
using ATool.Services;
using ATool.ViewModels;

namespace ATool;

internal static class Program
{
    /// <summary>DI 容器（App 启动时构建；视图层解析服务用）。</summary>
    public static IServiceProvider? Services { get; private set; }

    // 默认日志目录：%APPDATA%\ATool\logs（可在设置中修改，修改时重建 Logger）
    public static string LogDirectory { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATool", "logs");

    /// <summary>单实例锁：程序已在运行时新实例直接退出（防止旧进程的浮窗/托盘与新版并存）。</summary>
    private static Mutex? _singleInstance;

    /// <summary>开机自启模式（--autostart）：不弹主界面，只驻留托盘。</summary>
    public static bool AutoStartMode { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogger();
        AutoStartMode = args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        _singleInstance = new Mutex(true, @"Local\ATool_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Log.Information("ATool 已在运行，新实例退出（如需更新请先退出托盘旧实例）");
            return;
        }
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
        }
        finally
        {
            _singleInstance.ReleaseMutex();
            Log.CloseAndFlush();
        }
    }

    public static void ConfigureLogger(string? logDirectory = null)
    {
        if (logDirectory is not null) LogDirectory = logDirectory;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(LogDirectory, "atool-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90)
            .CreateLogger();
        Log.Information("日志已初始化: {Dir}", LogDirectory);
    }

    /// <summary>DI 容器装配（Presentation/Application/Infrastructure 三层）。</summary>
    public static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // 数据层（Db 先按默认路径构造，SettingsService 构造时纠正为配置路径）
        services.AddSingleton(new Db(SettingsService.DefaultDataPath()));
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<ReminderRepository>();
        services.AddSingleton<ApiKeyRepository>();
        services.AddSingleton<BalanceHistoryRepository>();
        services.AddSingleton<UsageLogRepository>();

        // 应用层服务
    services.AddSingleton<DeepSeekClient>();
    services.AddSingleton<BalanceService>();
    services.AddSingleton<SettingsService>();
    services.AddSingleton<ToastService>();
        services.AddSingleton<ReminderPopupService>();
        services.AddSingleton<ReminderSchedulerService>();
        services.AddSingleton<FloatReminderService>();
        services.AddSingleton<UsageTrackerService>();

        // 表现层 VM
    services.AddSingleton<ReminderEditViewModel>();
    services.AddSingleton<ReminderListViewModel>();
    services.AddSingleton<ReminderCalendarViewModel>();
    services.AddSingleton<ApiKeysViewModel>();
    services.AddSingleton<SettingsViewModel>();
    services.AddSingleton<BalanceChartViewModel>();
    services.AddSingleton<BalanceDetailViewModel>();
    services.AddSingleton<TimeMasterViewModel>();
    services.AddSingleton<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();
        Services = provider;
        return provider;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
