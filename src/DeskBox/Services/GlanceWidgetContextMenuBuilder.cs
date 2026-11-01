using DeskBox.Models;
using DeskBox.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Services;

internal static class GlanceWidgetContextMenuBuilder
{
    private static readonly GlanceDisplayElement[] DisplayElements =
    [
        GlanceDisplayElement.Time,
        GlanceDisplayElement.Date,
        GlanceDisplayElement.Year,
        GlanceDisplayElement.Weekday,
        GlanceDisplayElement.Calendar
    ];

    private static readonly GlanceLayoutMode[] Layouts =
    [
        GlanceLayoutMode.Immersive,
        GlanceLayoutMode.Centered,
        GlanceLayoutMode.Editorial,
        GlanceLayoutMode.Calendar
    ];

    public static void Append(
        MenuFlyout flyout,
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localizationService);

        flyout.Items.Add(CreateDisplayMenu(viewModel, localizationService));
        flyout.Items.Add(CreateLayoutMenu(viewModel, localizationService));
        flyout.Items.Add(new MenuFlyoutSeparator());
    }

    private static MenuFlyoutSubItem CreateDisplayMenu(
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        var menu = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Glance.Display.Title"),
            Icon = new FontIcon { Glyph = "\uE7B3" }
        };

        foreach (GlanceDisplayElement element in DisplayElements)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = localizationService.T(GetDisplayTextKey(element)),
                IsChecked = GlanceWidgetSettingsPolicy.IsDisplayElementVisible(
                    viewModel.Settings,
                    element)
            };
            item.Click += (_, _) =>
                _ = RunAsync(() => viewModel.SetDisplayElementAsync(
                    element,
                    item.IsChecked));
            menu.Items.Add(item);
        }

        return menu;
    }

    private static MenuFlyoutSubItem CreateLayoutMenu(
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        var menu = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Glance.Layout.Title"),
            Icon = new FontIcon { Glyph = "\uE80A" }
        };

        foreach (GlanceLayoutMode layout in Layouts)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = localizationService.T(GetLayoutTextKey(layout)),
                GroupName = "GlanceLayout",
                IsChecked = viewModel.Settings.Layout == layout
            };
            item.Click += (_, _) =>
                _ = RunAsync(() => viewModel.SetLayoutAsync(layout));
            menu.Items.Add(item);
        }

        return menu;
    }

    private static string GetDisplayTextKey(GlanceDisplayElement element) =>
        element switch
        {
            GlanceDisplayElement.Time => "Glance.Display.Time",
            GlanceDisplayElement.Date => "Glance.Display.Date",
            GlanceDisplayElement.Year => "Glance.Display.Year",
            GlanceDisplayElement.Weekday => "Glance.Display.Weekday",
            GlanceDisplayElement.Calendar => "Glance.Display.Calendar",
            _ => "Glance.Display.Title"
        };

    private static string GetLayoutTextKey(GlanceLayoutMode layout) =>
        layout switch
        {
            GlanceLayoutMode.Centered => "Glance.Layout.Centered",
            GlanceLayoutMode.Editorial => "Glance.Layout.Editorial",
            GlanceLayoutMode.Calendar => "Glance.Layout.Calendar",
            _ => "Glance.Layout.Immersive"
        };

    private static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            App.Log($"[GlanceContextMenu] Command failed: {ex}");
        }
    }
}
