using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ATool;

/// <summary>
/// MVVM 视图定位器：按 ViewModel 全名推导 View 类型（ATool.ViewModels.XxxViewModel → ATool.Views.XxxView）。
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;
        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "未找到视图: " + name };
    }

    public bool Match(object? data) => data is ObservableObject;
}
