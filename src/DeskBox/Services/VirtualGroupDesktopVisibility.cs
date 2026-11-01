using DeskBox.Models;
using DeskBox.Platform;

namespace DeskBox.Services;

/// <summary>
/// Hides and reveals desktop files referenced by virtual group widgets. Only
/// direct children of the user/public desktop are touched; files elsewhere
/// are merely referenced and keep their attributes.
/// </summary>
internal static class VirtualGroupDesktopVisibility
{
    internal static bool IsOnDesktop(string path)
    {
        try
        {
            string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(parent))
            {
                return false;
            }

            (string userDesktop, string publicDesktop) = FileService.GetDesktopPaths();
            return SamePath(parent, userDesktop) || SamePath(parent, publicDesktop);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets or clears the Hidden attribute on every desktop path in
    /// <paramref name="paths"/>, then refreshes the desktop once for the batch.
    /// </summary>
    internal static void SetHidden(IEnumerable<string> paths, bool hidden)
    {
        bool changed = false;
        foreach (string path in paths)
        {
            if (!IsOnDesktop(path))
            {
                continue;
            }

            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    continue;
                }

                FileAttributes current = File.GetAttributes(path);
                FileAttributes target = hidden
                    ? current | FileAttributes.Hidden
                    : current & ~FileAttributes.Hidden;
                if (target != current)
                {
                    File.SetAttributes(path, target);
                    changed = true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Log($"[VirtualGroup] Attribute change failed hidden={hidden} {ex.GetType().Name}");
            }
        }

        if (changed)
        {
            NotifyDesktop();
        }
    }

    internal static void HideAll(IEnumerable<WidgetConfig> widgets) =>
        SetHidden(CollectPaths(widgets), hidden: true);

    internal static void RestoreAll(IEnumerable<WidgetConfig> widgets) =>
        SetHidden(CollectPaths(widgets), hidden: false);

    private static IEnumerable<string> CollectPaths(IEnumerable<WidgetConfig> widgets) =>
        widgets
            .Where(widget => widget.IsVirtualGroup)
            .SelectMany(widget => widget.FilePaths.ToArray())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void NotifyDesktop()
    {
        (string userDesktop, string publicDesktop) = FileService.GetDesktopPaths();
        Win32Helper.NotifyShellDirectoryUpdated(userDesktop);
        Win32Helper.NotifyShellDirectoryUpdated(publicDesktop);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
