using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls;

public static class FileWidgetFolderOpenBehaviorMenuBuilder
{
    public static MenuFlyoutSubItem Create(
        WidgetConfig config,
        LocalizationService localization,
        Action<string?> setOverride)
    {
        string? currentOverride =
            FileWidgetFolderOpenBehaviorNames.GetOverride(config);
        var menu = new MenuFlyoutSubItem
        {
            Text = localization.T("Widget.FolderNavigation.MenuTitle"),
            Icon = new FontIcon { Glyph = "\uE838" }
        };

        menu.Items.Add(CreateOption(
            localization.T("Widget.FolderNavigation.FollowGlobal"),
            currentOverride is null,
            () => setOverride(null)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateOption(
            localization.T(
                "Settings.FileWidget.FolderOpenBehavior.Explorer"),
            string.Equals(
                currentOverride,
                FileWidgetFolderOpenBehaviorNames.Explorer,
                StringComparison.Ordinal),
            () => setOverride(
                FileWidgetFolderOpenBehaviorNames.Explorer)));
        menu.Items.Add(CreateOption(
            localization.T(
                "Settings.FileWidget.FolderOpenBehavior.Embedded"),
            string.Equals(
                currentOverride,
                FileWidgetFolderOpenBehaviorNames.Embedded,
                StringComparison.Ordinal),
            () => setOverride(
                FileWidgetFolderOpenBehaviorNames.Embedded)));

        return menu;
    }

    private static ToggleMenuFlyoutItem CreateOption(
        string text,
        bool isChecked,
        Action select)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            IsChecked = isChecked
        };
        item.Click += (_, _) => select();
        return item;
    }
}
