using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ATool.Data;
using ATool.Services;
using ATool.ViewModels;
using ATool.Views;

namespace ATool;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Program.BuildServices();

            // 初始化数据库 schema（默认路径；设置中配置了其他路径时 SettingsService 会纠正）
            var db = services.GetRequiredService<Db>();
            db.InitializeSchema();

            var settings = services.GetRequiredService<SettingsService>();
            var vm = services.GetRequiredService<MainWindowViewModel>();
            vm.Chart.Refresh();

            DataContext = vm; // 托盘命令绑定 Application.DataContext
            desktop.MainWindow = new MainWindow { DataContext = vm };

            // 启动后台服务：提醒调度 + 余额自动刷新
            var scheduler = services.GetRequiredService<ReminderSchedulerService>();
            scheduler.Start();
            var balance = services.GetRequiredService<BalanceService>();
            balance.SetAutoRefreshMinutes(settings.GetRefreshMinutes());
            balance.StartAutoRefresh();
            var toast = services.GetRequiredService<ToastService>();
            toast.Initialize();
            // 桌面提醒浮窗（设置开启才显示；绑定主窗口以检测全屏状态）
            var floatReminder = services.GetRequiredService<FloatReminderService>();
            floatReminder.SetMainWindow(desktop.MainWindow);
            floatReminder.Apply();

            desktop.Exit += (_, _) =>
            {
                scheduler.Dispose();
                toast.Shutdown();
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
