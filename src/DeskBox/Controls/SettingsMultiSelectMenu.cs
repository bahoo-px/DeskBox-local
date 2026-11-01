using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls;

internal static class SettingsMultiSelectMenu
{
    public static void Show(
        DropDownButton button,
        IReadOnlyList<string> values,
        Func<string, string> displayValue,
        Func<string, bool> isSelected,
        Func<string, bool> canToggle,
        Action<string> toggle)
    {
        double flyoutWidth = Math.Max(220, Math.Max(button.ActualWidth, button.MinWidth));
        var flyout = new MenuFlyout
        {
            ShouldConstrainToRootBounds = false
        };

        foreach (string value in values)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Tag = value,
                Text = displayValue(value),
                IsChecked = isSelected(value),
                IsEnabled = canToggle(value),
                MinWidth = flyoutWidth
            };
            item.Click += (_, _) =>
            {
                toggle(value);
                foreach (ToggleMenuFlyoutItem menuItem in flyout.Items.OfType<ToggleMenuFlyoutItem>())
                {
                    if (menuItem.Tag is not string itemValue)
                    {
                        continue;
                    }

                    menuItem.IsChecked = isSelected(itemValue);
                    menuItem.IsEnabled = canToggle(itemValue);
                }
            };
            flyout.Items.Add(item);
        }

        flyout.ShowAt(button);
    }
}
