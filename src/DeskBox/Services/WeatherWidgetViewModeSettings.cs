using DeskBox.Models;

namespace DeskBox.Services;

internal static class WeatherWidgetViewModeSettings
{
    internal const string MetadataKey = "Weather.ViewMode";
    internal const string DayValue = "Day";
    internal const string WeekValue = "Week";

    internal static bool TryGetWeekView(
        WidgetConfig config,
        out bool useWeekView)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Metadata ??= [];
        if (config.Metadata.TryGetValue(
                MetadataKey,
                out string? value) &&
            value is DayValue or WeekValue)
        {
            useWeekView = value == WeekValue;
            return true;
        }

        useWeekView = false;
        return false;
    }

    internal static bool SetWeekView(
        WidgetConfig config,
        bool useWeekView)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Metadata ??= [];
        string value = useWeekView ? WeekValue : DayValue;
        if (config.Metadata.TryGetValue(
                MetadataKey,
                out string? current) &&
            string.Equals(current, value, StringComparison.Ordinal))
        {
            return false;
        }

        config.Metadata[MetadataKey] = value;
        return true;
    }
}
