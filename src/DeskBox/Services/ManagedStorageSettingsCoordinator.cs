using DeskBox.Contracts;
using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>
/// Settings writer for managed-storage preferences. Existing-data migrations
/// commit the root and widget mappings through the verified migration transaction;
/// the settings window only re-projects that committed value. SetDefaultRootPath
/// remains available for preference-only callers and initial configuration.
/// </summary>
public sealed class ManagedStorageSettingsCoordinator : IManagedStorageSettings
{
    private readonly SettingsService _settings;
    private bool _stopped;

    internal bool IsStopped => _stopped;

    public ManagedStorageSettingsCoordinator(SettingsService settings)
    {
        _settings = settings;
    }

    public ManagedStoragePresentationSettings ReadManagedStoragePresentation()
    {
        FileWidgetSettingsSlice fileWidget = _settings.Settings.FileWidget;
        return new ManagedStoragePresentationSettings(
            NormalizeDropAction(fileWidget.ManagedDropAction),
            NormalizeDragOutAction(fileWidget.ManagedDragOutAction),
            fileWidget.DragOutModifierTipEnabled,
            fileWidget.DragOutResultHintEnabled,
            SettingsService.NormalizeManagedStorageRootPath(
                fileWidget.DefaultManagedStorageRootPath));
    }

    public string ReadDefaultRootPath() =>
        _settings.Settings.FileWidget.DefaultManagedStorageRootPath;

    public string SetDefaultRootPath(string path)
    {
        ThrowIfStopped();
        string normalizedPath = SettingsService.NormalizeManagedStorageRootPath(path);
        _settings.Settings.FileWidget.DefaultManagedStorageRootPath = normalizedPath;
        _settings.SaveDebounced();
        return normalizedPath;
    }

    public bool SetManagedDropAction(string? action)
    {
        ThrowIfStopped();
        string normalized = NormalizeDropAction(action);
        FileWidgetSettingsSlice fileWidget = _settings.Settings.FileWidget;
        if (string.Equals(
                fileWidget.ManagedDropAction,
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        fileWidget.ManagedDropAction = normalized;
        _settings.SaveDebounced();
        return true;
    }

    public bool SetManagedDragOutAction(string? action)
    {
        ThrowIfStopped();
        string normalized = NormalizeDragOutAction(action);
        FileWidgetSettingsSlice fileWidget = _settings.Settings.FileWidget;
        if (string.Equals(
                fileWidget.ManagedDragOutAction,
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        fileWidget.ManagedDragOutAction = normalized;
        _settings.SaveDebounced();
        return true;
    }

    public bool SetDragOutModifierTipEnabled(bool enabled)
    {
        ThrowIfStopped();
        FileWidgetSettingsSlice fileWidget = _settings.Settings.FileWidget;
        if (fileWidget.DragOutModifierTipEnabled == enabled)
        {
            return false;
        }

        fileWidget.DragOutModifierTipEnabled = enabled;
        _settings.SaveDebounced();
        return true;
    }

    public bool SetDragOutResultHintEnabled(bool enabled)
    {
        ThrowIfStopped();
        FileWidgetSettingsSlice fileWidget = _settings.Settings.FileWidget;
        if (fileWidget.DragOutResultHintEnabled == enabled)
        {
            return false;
        }

        fileWidget.DragOutResultHintEnabled = enabled;
        _settings.SaveDebounced();
        return true;
    }

    private static string NormalizeDropAction(string? action) => action switch
    {
        ManagedDropActions.Copy => ManagedDropActions.Copy,
        ManagedDropActions.FollowWindows => ManagedDropActions.FollowWindows,
        _ => ManagedDropActions.Move
    };

    // Drag-out falls back to FollowWindows, not Move: advertising no
    // preferred effect keeps the receiver's native default and never hands
    // a third-party target the token that lets it remove the source.
    private static string NormalizeDragOutAction(string? action) => action switch
    {
        ManagedDropActions.Copy => ManagedDropActions.Copy,
        ManagedDropActions.Move => ManagedDropActions.Move,
        _ => ManagedDropActions.FollowWindows
    };

    private void ThrowIfStopped() => ObjectDisposedException.ThrowIf(_stopped, this);

    internal void Stop() => _stopped = true;
}
