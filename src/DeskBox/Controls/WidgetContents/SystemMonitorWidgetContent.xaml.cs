using DeskBox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DeskBox.Controls.WidgetContents;

public sealed partial class SystemMonitorWidgetContent : UserControl
{
    private readonly SystemMonitorWidgetViewModel _viewModel;

    public SystemMonitorWidgetContent(SystemMonitorWidgetViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.RequestLaunchAsync();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.RequestStopAsync();
    }
}

/// <summary>
/// Maps the widget status level (0 dashboard down, 1 stopped, 2 running,
/// 3 busy) to the header dot brush.
/// </summary>
public sealed partial class SystemMonitorStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush DashboardDownBrush = new(Windows.UI.Color.FromArgb(255, 138, 138, 138));
    private static readonly SolidColorBrush StoppedBrush = new(Windows.UI.Color.FromArgb(255, 229, 162, 60));
    private static readonly SolidColorBrush RunningBrush = new(Windows.UI.Color.FromArgb(255, 63, 185, 80));
    private static readonly SolidColorBrush BusyBrush = new(Windows.UI.Color.FromArgb(255, 96, 165, 250));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int level
            ? level switch
            {
                ViewModels.SystemMonitorWidgetViewModel.StatusDashboardDown => DashboardDownBrush,
                ViewModels.SystemMonitorWidgetViewModel.StatusStopped => StoppedBrush,
                ViewModels.SystemMonitorWidgetViewModel.StatusRunning => RunningBrush,
                ViewModels.SystemMonitorWidgetViewModel.StatusBusy => BusyBrush,
                _ => StoppedBrush
            }
            : StoppedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class SystemMonitorBusyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
