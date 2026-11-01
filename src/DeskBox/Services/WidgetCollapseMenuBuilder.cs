using DeskBox.Models;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Services;

internal static class WidgetCollapseMenuBuilder
{
    public static MenuFlyoutSubItem Create(
        WidgetConfig config,
        string defaultBehavior,
        string globalExpansionDirection,
        LocalizationService localizationService,
        Action<WidgetCollapseBehavior> applyBehavior,
        Action<string?> applyExpansionDirection,
        Action resetCompactWidth)
    {
        WidgetCollapseBehavior selectedBehavior = WidgetCollapseBehaviorNames.GetOverride(config);
        var subItem = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Widget.CollapseBehavior.Title"),
            Icon = new FontIcon { Glyph = "\uE73F" }
        };

        foreach (WidgetCollapseBehavior behavior in new[]
                 {
                     WidgetCollapseBehavior.System,
                     WidgetCollapseBehavior.Expanded,
                     WidgetCollapseBehavior.Click,
                     WidgetCollapseBehavior.Smart
                 })
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = GetText(behavior, defaultBehavior, localizationService),
                IsChecked = selectedBehavior == behavior
            };
            item.Click += (_, _) => applyBehavior(behavior);
            subItem.Items.Add(item);
        }

        subItem.Items.Add(new MenuFlyoutSeparator());

        string? selectedDirection =
            WidgetCompactExpansionDirectionPolicy.GetOverride(config);
        string globalDirectionText = GetDirectionText(
            SettingsService.NormalizeWidgetCompactExpansionDirection(
                globalExpansionDirection),
            localizationService);
        foreach (string? direction in new string?[]
                 {
                     null,
                     SettingsService.WidgetCompactExpansionDirectionAuto,
                     SettingsService.WidgetCompactExpansionDirectionDown,
                     SettingsService.WidgetCompactExpansionDirectionUp
                 })
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = direction is null
                    ? localizationService.Format(
                        "Widget.Compact.ExpansionDirection.FollowGlobal",
                        globalDirectionText)
                    : GetDirectionText(direction, localizationService),
                IsChecked = selectedDirection == direction
            };
            string? captured = direction;
            item.Click += (_, _) => applyExpansionDirection(captured);
            subItem.Items.Add(item);
        }

        subItem.Items.Add(new MenuFlyoutSeparator());
        var resetWidthItem = new MenuFlyoutItem
        {
            Text = localizationService.T("Widget.Compact.RestoreAutomaticWidth"),
            Icon = new FontIcon { Glyph = "\uE8A7" },
            IsEnabled = config.CompactWidth is not null
        };
        resetWidthItem.Click += (_, _) => resetCompactWidth();
        subItem.Items.Add(resetWidthItem);

        return subItem;
    }

    private static string GetText(
        WidgetCollapseBehavior behavior,
        string defaultBehavior,
        LocalizationService localizationService)
    {
        if (behavior != WidgetCollapseBehavior.System)
        {
            return localizationService.T(GetTextKey(behavior));
        }

        WidgetCollapseBehavior normalizedDefault = WidgetCollapseBehaviorNames.Normalize(
            defaultBehavior,
            WidgetCollapseBehavior.Expanded);
        return localizationService.Format(
            "Widget.CollapseBehavior.SystemWithDefault",
            localizationService.T(GetTextKey(normalizedDefault)));
    }

    private static string GetDirectionText(
        string direction,
        LocalizationService localizationService)
    {
        return localizationService.T(direction switch
        {
            SettingsService.WidgetCompactExpansionDirectionDown =>
                "Settings.Capsule.ExpansionDirection.Down",
            SettingsService.WidgetCompactExpansionDirectionUp =>
                "Settings.Capsule.ExpansionDirection.Up",
            _ => "Settings.Capsule.ExpansionDirection.Auto"
        });
    }

    private static string GetTextKey(WidgetCollapseBehavior behavior)
    {
        return behavior switch
        {
            WidgetCollapseBehavior.System => "Widget.CollapseBehavior.System",
            WidgetCollapseBehavior.Expanded => "Widget.CollapseBehavior.Expanded",
            WidgetCollapseBehavior.Smart => "Widget.CollapseBehavior.Smart",
            _ => "Widget.CollapseBehavior.Click"
        };
    }

}
