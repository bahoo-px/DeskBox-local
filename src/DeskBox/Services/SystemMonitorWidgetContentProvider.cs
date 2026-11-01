using DeskBox.Contracts;
using DeskBox.Controls.WidgetContents;
using DeskBox.Models;
using DeskBox.ViewModels;

namespace DeskBox.Services;

internal sealed class SystemMonitorWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.SystemMonitor;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        App.Log("[SystemMonitor] CreateDetachedContent begin");
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("SystemMonitor content requires a SystemMonitor widget config.", nameof(config));
        }

        try
        {
            var adapter = new SystemMonitorWidgetContentAdapter(
                config,
                context.LocalizationService);
            App.Log("[SystemMonitor] CreateDetachedContent done");
            return adapter;
        }
        catch (Exception ex)
        {
            App.Log($"[SystemMonitor] CreateDetachedContent FAILED: {ex}");
            throw;
        }
    }
}
