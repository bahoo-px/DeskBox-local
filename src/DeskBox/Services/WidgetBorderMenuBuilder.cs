using DeskBox.Models;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Services;

/// <summary>
/// Builds the per-widget border submenu: follow global, accent, neutral,
/// or none. Selection applies immediately through the caller's apply
/// callback, which owns persistence and the appearance refresh.
/// </summary>
internal static class WidgetBorderMenuBuilder
{
    public static MenuFlyoutSubItem Create(
        LocalizationService localization,
        WidgetConfig config,
        Action<string?> applyMode)
    {
        var menu = new MenuFlyoutSubItem
        {
            Text = localization.T("Widget.Border.Menu"),
            Icon = new FontIcon { Glyph = "\uE7E6" }
        };

        string current = WidgetBorderCustomization.GetModeOverride(config) ??
            WidgetBorderCustomization.ModeFollowGlobal;
        foreach ((string mode, string key) in new[]
        {
            (WidgetBorderCustomization.ModeFollowGlobal, "Widget.Border.UseGlobal"),
            (WidgetBorderCustomization.ModeAccent, "Widget.Border.Accent"),
            (WidgetBorderCustomization.ModeNeutral, "Widget.Border.Neutral"),
            (WidgetBorderCustomization.ModeNone, "Widget.Border.None")
        })
        {
            string selectedMode = mode;
            var item = new ToggleMenuFlyoutItem
            {
                Text = localization.T(key),
                IsChecked = mode == current
            };
            item.Click += (_, _) => applyMode(selectedMode);
            menu.Items.Add(item);
        }

        return menu;
    }
}
