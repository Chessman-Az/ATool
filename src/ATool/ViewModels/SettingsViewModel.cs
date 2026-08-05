using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>系统设置：数据/日志路径（迁移+回滚）、自动刷新间隔（≥5）、开机自启（Windows）。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string _dataPath = "";

    [ObservableProperty]
    private string _logPath = "";

    [ObservableProperty]
    private int _refreshMinutes = 30;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _autoStartSupported = OperatingSystem.IsWindows();

    [ObservableProperty]
    private string? _message;

    public SettingsViewModel(SettingsService settings) => _settings = settings;

    public void Load()
    {
        DataPath = _settings.DataPath;
        LogPath = _settings.LogPath;
        RefreshMinutes = _settings.GetRefreshMinutes();
        AutoStart = _settings.IsAutoStartEnabled();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _settings.SetRefreshMinutes(RefreshMinutes);
            _settings.SetAutoStart(AutoStart);
            Message = "设置已保存";
        }
        catch (Exception ex)
        {
            Message = $"保存失败：{ex.Message}";
        }
    }

    /// <summary>迁移数据目录到新路径；失败回滚（配置不变）。</summary>
    [RelayCommand]
    private void MigrateDataPath()
    {
        if (string.IsNullOrWhiteSpace(DataPath)) return;
        try
        {
            _settings.ChangeDataPath(DataPath);
            Message = "数据目录迁移成功";
        }
        catch (Exception ex)
        {
            Message = $"迁移失败（原配置未变）：{ex.Message}";
        }
    }
}
