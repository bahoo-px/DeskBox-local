using DeskBox.Contracts;
using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    private int _fileStackPreviewRefreshGeneration;
    private List<FileStackPreviewEntry> _fileStackPreviewEntries = [];

    /// <summary>
    /// Re-scans the widget config (and, for mapped folders, the disk) for
    /// file-stack rule-preview entries and pushes them onto the file-stack
    /// editor, which owns the rule matching and the preview texts. The scan
    /// itself needs the widget config and the file services, so it stays on
    /// this shell (batch 45 state-machine split: shell scans, editor
    /// projects).
    /// </summary>
    public async Task RefreshFileStackRulePreviewFromDiskAsync()
    {
        int generation = Interlocked.Increment(ref _fileStackPreviewRefreshGeneration);
        _fileStackSettings.MarkPreviewLoading();
        List<FileStackPreviewEntry> entries;
        try
        {
            entries = await Task.Run(
                () => BuildFileStackPreviewEntries(includeMappedFolders: true),
                _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isDisposed || generation != _fileStackPreviewRefreshGeneration)
        {
            return;
        }

        _fileStackPreviewEntries = entries;
        _fileStackSettings.UpdatePreviewEntries(entries);
    }

    private List<FileStackPreviewEntry> BuildFileStackPreviewEntries(
        bool includeMappedFolders)
    {
        var entries = new List<FileStackPreviewEntry>();
        foreach (WidgetConfig widget in _settingsService.Settings.Widgets.Where(
                     widget => widget.WidgetKind == WidgetKind.File && !widget.IsDisabled))
        {
            IEnumerable<string> paths = widget.Items.Select(item => item.Path);
            if (includeMappedFolders &&
                !string.IsNullOrWhiteSpace(widget.MappedFolderPath) &&
                FileService.TryResolveExistingPathForTraversal(
                    widget.MappedFolderPath,
                    out string mappedFolderTraversalPath))
            {
                paths = EnumerateFileStackPreviewPaths(mappedFolderTraversalPath);
            }

            foreach (string path in paths
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new FileStackPreviewEntry(
                    widget.Name,
                    path,
                    Directory.Exists(path) ? string.Empty : Path.GetExtension(path)));
            }
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateFileStackPreviewPaths(string folderPath)
    {
        var folders = new List<string> { folderPath };
        var (userDesktop, publicDesktop) = FileService.GetDesktopPaths();
        if (folderPath.Equals(userDesktop, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(publicDesktop))
        {
            folders.Add(publicDesktop);
        }

        var paths = new List<string>();
        foreach (string folder in folders)
        {
            try
            {
                paths.AddRange(Directory
                    .EnumerateFileSystemEntries(folder)
                    .Where(IsVisibleFileStackPreviewPath));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return paths
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static bool IsVisibleFileStackPreviewPath(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) == 0;
        }
        catch
        {
            return false;
        }
    }
}
