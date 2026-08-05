using Avalonia.Controls;
using ATool.ViewModels;

namespace ATool.Views;

public partial class ReminderPopupWindow : Window
{
    public ReminderPopupWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is ReminderPopupViewModel vm)
                vm.Closed += Close;
        };
    }
}
