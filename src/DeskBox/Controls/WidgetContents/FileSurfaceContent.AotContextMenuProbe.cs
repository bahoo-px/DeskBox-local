#if DESKBOX_NATIVE_AOT
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls.WidgetContents;

/// <summary>
/// Shared probe helpers for the AOT menu smoke fixtures against the
/// CommandBarFlyout-based item menus (primary strip + secondary rows).
/// </summary>
internal static class AotContextMenuProbe
{
    internal static AppBarButton FindButton(
        CommandBarFlyout flyout,
        string label) =>
        flyout.PrimaryCommands.OfType<AppBarButton>()
            .Concat(flyout.SecondaryCommands.OfType<AppBarButton>())
            .Single(button => string.Equals(
                button.Label,
                label,
                StringComparison.Ordinal));

    internal static int Count(CommandBarFlyout flyout) =>
        flyout.PrimaryCommands.Count + flyout.SecondaryCommands.Count;

    internal static int IndexOf(
        CommandBarFlyout flyout,
        AppBarButton target) =>
        Commands(flyout).ToList().IndexOf(target);

    internal static IReadOnlyList<AotContextMenuEntrySnapshot> Snapshot(
        CommandBarFlyout flyout,
        AppBarButton target) =>
        Commands(flyout)
            .Select((element, index) => new AotContextMenuEntrySnapshot(
                index,
                element.GetType().Name,
                element is AppBarButton button ? button.Label : string.Empty,
                element is not Control control || control.IsEnabled,
                ReferenceEquals(element, target)))
            .ToArray();

    internal static IInvokeProvider InvokeProvider(Control control)
    {
        if (FrameworkElementAutomationPeer.CreatePeerForElement(control)
                ?.GetPattern(PatternInterface.Invoke)
            is IInvokeProvider provider)
        {
            return provider;
        }

        throw new InvalidOperationException(
            "The product menu entry did not expose Invoke automation.");
    }

    private static IEnumerable<ICommandBarElement> Commands(
        CommandBarFlyout flyout) =>
        flyout.PrimaryCommands.Concat(flyout.SecondaryCommands);
}

internal sealed record AotContextMenuEntrySnapshot(
    int Index,
    string ItemType,
    string Text,
    bool IsEnabled,
    bool IsTarget);
#endif
