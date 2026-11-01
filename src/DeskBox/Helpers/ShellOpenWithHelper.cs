using System.Runtime.InteropServices;
using DeskBox.Platform;

namespace DeskBox.Helpers;

/// <summary>
/// One recommended "Open with" application, backed by a live Shell
/// association handler. The handler must stay alive until the menu entry is
/// invoked or disposed, matching the Shell lifetime rule for menu hosts.
/// </summary>
public sealed class OpenWithHandlerEntry : IDisposable
{
    private ShellOpenWithInterop.ShellOpenWithAssocHandler? _handler;

    internal OpenWithHandlerEntry(
        ShellOpenWithInterop.ShellOpenWithAssocHandler handler,
        string displayName,
        string? iconSourcePath,
        int iconSourceIndex)
    {
        _handler = handler;
        UiName = displayName;
        IconSourcePath = iconSourcePath;
        IconSourceIndex = iconSourceIndex;
    }

    public string UiName { get; }

    /// <summary>Icon location from IAssocHandler::GetIconLocation; null
    /// when the handler has no icon path (the entry stays text-only).</summary>
    public string? IconSourcePath { get; }

    public int IconSourceIndex { get; }

    /// <summary>
    /// Opens <paramref name="filePath"/> with this application through the
    /// Shell (works for desktop and store applications). Must be called on
    /// the UI thread from a user gesture.
    /// </summary>
    public bool TryInvoke(string filePath)
    {
        if (_handler is null || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!ShellOpenWithInterop.TryCreateDataObjectForPath(
                filePath,
                out IntPtr dataObject))
        {
            return false;
        }

        try
        {
            _handler.Invoke(dataObject);
            return true;
        }
        catch (Exception ex)
        {
            App.Log(
                $"[OpenWith] invoke failed handler='{UiName}' " +
                $"file='{filePath}': {ex.Message}");
            return false;
        }
        finally
        {
            ShellOpenWithInterop.ReleaseDataObject(ref dataObject);
        }
    }

    public void Dispose()
    {
        if (_handler is { } handler)
        {
            _handler = null;
            try
            {
                Marshal.ReleaseComObject(handler);
            }
            catch (COMException)
            {
                // The RCW is already gone; nothing to release.
            }
        }
    }
}

/// <summary>
/// Enumerates the recommended "Open with" applications for a file and shows
/// the system "choose another app" dialog. Uses only documented Shell
/// association APIs (SHAssocEnumHandlers); the full classic Shell menu with
/// third-party handlers still belongs to <see cref="ShellContextMenuProxy"/>.
/// </summary>
public static class ShellOpenWithHelper
{
    private const int MaxEntries = 12;

    public static IReadOnlyList<OpenWithHandlerEntry> GetRecommendedApps(
        string filePath)
    {
        string? extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            return [];
        }

        try
        {
            ShellOpenWithInterop.SHAssocEnumHandlers(
                extension,
                ShellOpenWithInterop.AssocFilterRecommended,
                out ShellOpenWithInterop.ShellOpenWithEnumHandlers handlers);

            var entries = new List<OpenWithHandlerEntry>();
            var seen = new HashSet<string>(StringComparer.CurrentCulture);
            var buffer = new IntPtr[1];
            while (entries.Count < MaxEntries)
            {
                handlers.Next(1, buffer, out uint fetched);
                if (fetched == 0 || buffer[0] == IntPtr.Zero)
                {
                    break;
                }

                IntPtr handlerPtr = buffer[0];
                try
                {
                    var handler = Marshal.GetObjectForIUnknown(handlerPtr)
                        as ShellOpenWithInterop.ShellOpenWithAssocHandler;
                    if (handler is null)
                    {
                        Marshal.Release(handlerPtr);
                        continue;
                    }

                    // The RCW owns one reference; drop the raw one from Next.
                    Marshal.Release(handlerPtr);

                    handler.GetUIName(out IntPtr namePtr);
                    string? displayName = PtrToStringOrNull(namePtr);
                    if (displayName is null)
                    {
                        Marshal.ReleaseComObject(handler);
                        continue;
                    }

                    string? iconPath = null;
                    int iconIndex = 0;
                    try
                    {
                        handler.GetIconLocation(
                            out IntPtr iconPtr,
                            out iconIndex);
                        iconPath = PtrToStringOrNull(iconPtr);
                    }
                    catch (COMException)
                    {
                        // No icon location is fine; the entry stays
                        // text-only.
                    }

                    if (seen.Add(displayName))
                    {
                        entries.Add(new OpenWithHandlerEntry(
                            handler,
                            displayName,
                            iconPath,
                            iconIndex));
                    }
                    else
                    {
                        Marshal.ReleaseComObject(handler);
                    }
                }
                catch (COMException)
                {
                    // A single broken association must not take the menu down.
                }
            }

            return entries;
        }
        catch (Exception ex)
        {
            App.Log(
                $"[OpenWith] enumeration failed ext='{extension}': " +
                ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Whether the type can meaningfully be opened with another app:
    /// executables (the Open With dialog refuses them outright) and
    /// shortcuts (.lnk/.url — Explorer opens the target instead) are
    /// hidden, matching Explorer. Extension-less files DO offer Open With
    /// natively (picking a handler is the point), so only these families
    /// are filtered.
    /// </summary>
    public static bool SupportsOpenWith(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string extension = Path.GetExtension(filePath);
        return !ElevatedFileLauncher.ExecutableExtensions.Contains(extension) &&
            !ShortcutHelper.IsShortcutPath(filePath);
    }

    /// <summary>
    /// Shows the system Open With dialog for the file via the documented
    /// <c>openas</c> verb.
    /// </summary>
    public static bool ShowChooseAppDialog(
        IntPtr ownerWindow,
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            return ShellOpenWithInterop.TryShowOpenAsDialog(
                ownerWindow,
                filePath);
        }
        catch (Exception ex)
        {
            App.Log(
                $"[OpenWith] choose-app dialog failed file='{filePath}': " +
                ex.Message);
            return false;
        }
    }

    private static string? PtrToStringOrNull(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            string? value = Marshal.PtrToStringUni(pointer);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        finally
        {
            ShellOpenWithInterop.CoTaskMemFree(pointer);
        }
    }
}
