using DeskBox.Contracts;
using DeskBox.Controls.WidgetContents;
using DeskBox.Models;

namespace DeskBox.Services;

internal sealed class WeatherWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Weather;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Weather content requires a Weather widget config.", nameof(config));
        }

        WeatherService? weatherService = null;
#if DESKBOX_NATIVE_AOT && DESKBOX_AOT_SMOKE_HARNESS
        weatherService = AotWeatherSurfaceFixture.TryCreateService(config);
#endif
        return new WeatherWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService,
            weatherService);
    }
}
