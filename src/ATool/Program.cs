using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ATool.Data;
using ATool.Services;
using ATool.ViewModels;

namespace ATool;

internal static class Program
{
    // 默认日志目录：%APPDATA%\ATool\logs（可在设置中修改，修改时重建 Logger）
    public static string LogDirectory { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATool", "logs");

    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogger();
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

        // 应用层服务
        services.AddSingleton<DeepSeekClient>();
        services.AddSingleton<BalanceService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ReminderPopupService>();
        services.AddSingleton<ReminderSchedulerService>();

        // 表现层 VM
        services.AddSingleton<ReminderEditViewModel>();
        services.AddSingleton<ReminderListViewModel>();
        services.AddSingleton<ApiKeysViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
