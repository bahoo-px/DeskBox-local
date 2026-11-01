using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

/// <summary>
/// Virtual group mode: the widget references files by absolute path
/// (<see cref="WidgetConfig.FilePaths"/>) and never moves or copies them, so
/// project files (video edit projects, media) keep their physical location.
/// Desktop files are hidden while referenced and revealed when released, on
/// mode switch, and on application exit.
/// </summary>
public partial class WidgetViewModel
{
    public bool IsVirtualGroup => Config.IsVirtualGroup;

    /// <summary>
    /// Switches virtual group mode, persists it and reloads the item list from
    /// the matching source (referenced paths or the managed folder).
    /// </summary>
    public async Task SetVirtualGroupAsync(bool enabled)
    {
        if (_isDisposed || Config.IsVirtualGroup == enabled)
        {
            return;
        }

        string[] referenced = Config.FilePaths.ToArray();
        Config.IsVirtualGroup = enabled;
        _settingsService.UpdateWidget(Config);
        OnPropertyChanged(nameof(IsVirtualGroup));
        await Task.Run(() => VirtualGroupDesktopVisibility.SetHidden(referenced, hidden: enabled));
        if (string.IsNullOrEmpty(CurrentFolderPath))
        {
            return;
        }

        await ConfigureFolderWatchersAsync(CurrentFolderPath);
        await ReloadFolderContentsAsync(CurrentFolderPath);
    }

    private static string NormalizeVirtualPath(string path) => Path.GetFullPath(path);

    /// <summary>
    /// Records the dropped paths in <see cref="WidgetConfig.FilePaths"/> and
    /// shows them. Nothing is moved; desktop files are hidden in one batch.
    /// The caller receives an empty "moved" list so source widgets and the
    /// desktop are left untouched.
    /// </summary>
    private async Task<IReadOnlyList<string>> ImportVirtualPathsAsync(
        IReadOnlyList<string> paths,
        int? preferredManualIndex,
        CancellationToken cancellationToken)
    {
        var added = new List<string>();
        foreach (string path in paths)
        {
            string fullPath;
            try
            {
                fullPath = NormalizeVirtualPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                continue;
            }

            if (Config.FilePaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Config.FilePaths.Add(fullPath);
            added.Add(fullPath);
        }

        if (added.Count == 0)
        {
            return [];
        }

        _settingsService.UpdateWidget(Config);
        await Task.Run(() => VirtualGroupDesktopVisibility.SetHidden(added, hidden: true));
        int? nextIndex = preferredManualIndex;
        using (EnterItemMutationScope())
        {
            foreach (string fullPath in added)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RecordFileAddedAt(fullPath, DateTimeOffset.Now);
                if (await UpsertFolderItemAsync(fullPath, nextIndex) && nextIndex.HasValue)
                {
                    nextIndex++;
                }
            }
        }

        return [];
    }

    /// <summary>
    /// Loads the referenced paths. Entries whose file is currently missing are
    /// skipped for display but stay in <see cref="WidgetConfig.FilePaths"/>, so
    /// a transient disappearance never empties the box.
    /// </summary>
    private async Task<bool> LoadVirtualGroupContentsAsync(
        CancellationToken cancellationToken,
        Action? beforeItemsReplaced)
    {
        var items = new List<WidgetItem>();
        foreach (string path in Config.FilePaths.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            WidgetItem? item = await _fileService.TryCreateWidgetItemAsync(
                path,
                hideShortcutArrowOverlay: _hideShortcutArrowOverlay,
                showImageFilesAsIcons: _showImageFilesAsIcons,
                showFileExtensions: _showFileExtensions,
                hideShortcutExtensionWhenShowingFileExtensions: _hideShortcutExtensionWhenShowingFileExtensions,
                loadIcon: false,
                loadFolderItemCount: false,
                loadShortcutTarget: false,
                includeHidden: true);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_itemMutationBatchDepth > 0)
        {
            _pendingFolderRefreshAfterBatch = true;
            MarkItemMutationBatchDirty();
            return false;
        }

        ApplyPersistedAddedTimes(items);
        beforeItemsReplaced?.Invoke();
        SyncFolderItems(items);
        SortItems();
        StartItemHydration();
        return true;
    }

    /// <summary>
    /// Physical delete: sends the real files to the Recycle Bin, then drops
    /// the references of every file that is actually gone.
    /// </summary>
    public async Task<FileDeleteBatchResult> DeleteItemsAsync(
        IEnumerable<WidgetItem> items,
        bool recycle = true,
        IntPtr ownerHandle = default)
    {
        List<WidgetItem> targets = items
            .Where(item => item is not null)
            .Distinct()
            .ToList();
        FileDeleteBatchResult result = await DeleteItemsCoreAsync(targets, recycle, ownerHandle);
        if (Config.IsVirtualGroup)
        {
            string[] gone = targets
                .Select(item => item.Path)
                .Where(path => !File.Exists(path) && !Directory.Exists(path))
                .ToArray();
            if (gone.Length > 0)
            {
                Config.FilePaths.RemoveAll(path =>
                    gone.Contains(path, StringComparer.OrdinalIgnoreCase));
                _settingsService.UpdateWidget(Config, notifySubscribers: false);
            }
        }

        return result;
    }

    /// <summary>
    /// "Move out of box": removes only the references and reveals any desktop
    /// file again. Returns the number of released entries.
    /// </summary>
    private int RemoveVirtualReferences(IEnumerable<WidgetItem> items)
    {
        string[] paths = items
            .Where(item => item is not null)
            .Select(item => item.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            return 0;
        }

        Config.FilePaths.RemoveAll(path =>
            paths.Contains(path, StringComparer.OrdinalIgnoreCase));
        using (EnterItemMutationScope())
        {
            foreach (string path in paths)
            {
                RemoveItemByPath(path);
            }
        }

        RemoveStackMemberOverridePaths(paths);
        _settingsService.UpdateWidget(Config, notifySubscribers: false);
        _ = Task.Run(() => VirtualGroupDesktopVisibility.SetHidden(paths, hidden: false));
        return paths.Length;
    }

    /// <summary>
    /// Drag-out release: drops the references for the given paths and reveals
    /// desktop files again. Files are never moved.
    /// </summary>
    public int ReleaseVirtualPaths(IEnumerable<string> paths)
    {
        if (!Config.IsVirtualGroup)
        {
            return 0;
        }

        HashSet<string> wanted = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        WidgetItem[] items = Items
            .Where(item => wanted.Contains(item.Path))
            .ToArray();
        return RemoveVirtualReferences(items);
    }

    /// <summary>Keeps the stored reference in step with a physical rename.</summary>
    private void UpdateVirtualReference(string oldPath, string newPath)
    {
        if (!Config.IsVirtualGroup)
        {
            return;
        }

        int index = Config.FilePaths.FindIndex(
            path => string.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        Config.FilePaths[index] = newPath;
        _settingsService.UpdateWidget(Config, notifySubscribers: false);
    }
}
