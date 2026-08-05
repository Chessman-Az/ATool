using Avalonia.Controls;

namespace ATool.Views;

public partial class PeakHourWindow : Window
{
    public PeakHourWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.PeakHourViewModel();
    }
}
