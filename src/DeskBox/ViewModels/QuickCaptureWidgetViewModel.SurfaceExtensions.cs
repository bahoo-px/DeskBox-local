using DeskBox.Services;

namespace DeskBox.ViewModels;

/// <summary>
/// Surface-host helpers for translating a window-independent drop payload
/// into the richer Quick Capture attachment model.
/// </summary>
internal static class QuickCaptureWidgetViewModelSurfaceExtensions
{
    public static Task<QuickCaptureItemViewModel?> AddAttachmentsAsync(
        this QuickCaptureWidgetViewModel viewModel,
        QuickCaptureItemViewModel? targetItem,
        IReadOnlyList<string> droppedFiles)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(droppedFiles);

        DroppedFilePath[] paths = droppedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new DroppedFilePath(
                path,
                Path.GetFileName(path),
                ForceManagedCopy: false))
            .ToArray();

        return targetItem is null
            ? viewModel.AddItemWithAttachmentsAsync(paths)
            : viewModel.AddAttachmentsAsync(targetItem, paths);
    }
}
