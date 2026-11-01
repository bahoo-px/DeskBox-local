using DeskBox.Models;
using DeskBox.Services;
using DeskBox.ViewModels;

namespace DeskBox.Controls.WidgetContents;

public sealed class SystemMonitorWidgetContentAdapter : WidgetContentAdapterBase
{
    public SystemMonitorWidgetContentAdapter(
        WidgetConfig config,
        LocalizationService localizationService,
        SystemMonitorService? service = null,
        System.Func<SystemMonitorWidgetViewModel, Microsoft.UI.Xaml.FrameworkElement>? viewFactory = null)
        : this(
            config,
            new SystemMonitorWidgetViewModel(config, localizationService, service),
            viewFactory)
    {
    }

    private SystemMonitorWidgetContentAdapter(
        WidgetConfig config,
        SystemMonitorWidgetViewModel viewModel,
        System.Func<SystemMonitorWidgetViewModel, Microsoft.UI.Xaml.FrameworkElement>? viewFactory)
        : base(
            config,
            () =>
            {
                App.Log("[SystemMonitor] View factory invoked");
                try
                {
                    var element = (viewFactory ?? (vm => new SystemMonitorWidgetContent(vm)))(viewModel);
                    App.Log($"[SystemMonitor] View created: {element.GetType().Name}");
                    return element;
                }
                catch (Exception ex)
                {
                    App.Log($"[SystemMonitor] View factory FAILED: {ex}");
                    return new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = "SM VIEW FAIL: " + ex.GetType().Name + ": " + ex.Message,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        FontSize = 12,
                        Margin = new Microsoft.UI.Xaml.Thickness(8)
                    };
                }
            })
    {
        ViewModel = viewModel;
    }

    public SystemMonitorWidgetViewModel ViewModel { get; }

    public override async Task InitializeAsync()
    {
        App.Log("[SystemMonitor] InitializeAsync begin");
        try
        {
            await ViewModel.InitializeAsync();
            App.Log("[SystemMonitor] InitializeAsync done");
        }
        catch (Exception ex)
        {
            App.Log($"[SystemMonitor] InitializeAsync FAILED: {ex}");
            throw;
        }
    }

    public override Task RefreshAsync()
    {
        return ViewModel.RefreshAsync();
    }

    public override void ApplyAppearance()
    {
        ViewModel.ApplyAppearance();
    }

    public override void OnActivated()
    {
        ViewModel.OnActivated();
    }

    public override void OnDeactivated()
    {
        ViewModel.OnDeactivated();
    }

    public override void OnWindowVisibilityChanged(bool visible)
    {
        ViewModel.OnWindowVisibilityChanged(visible);
    }

    public override void OnWindowRevealCompleted()
    {
        ViewModel.OnWindowRevealCompleted();
    }

    protected override void Dispose(bool disposing)
    {
        ViewModel.Dispose();
    }
}
