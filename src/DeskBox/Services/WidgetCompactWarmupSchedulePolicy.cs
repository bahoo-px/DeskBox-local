using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>
/// Keeps cold-start capsule realization away from the first application paint
/// while favoring the built-in surfaces that have the largest first-layout
/// cost. Pointer-driven work bypasses this schedule in WidgetWindowBase.
/// </summary>
internal static class WidgetCompactWarmupSchedulePolicy
{
    public static int GetInitialDelayMilliseconds(WidgetKind kind) => kind switch
    {
        WidgetKind.QuickCapture => 220,
        WidgetKind.Weather => 260,
        WidgetKind.Search => 300,
        WidgetKind.Music => 340,
        WidgetKind.Todo => 380,
        WidgetKind.File => 420,
        WidgetKind.SystemMonitor => 460,
        _ => 500
    };
}
