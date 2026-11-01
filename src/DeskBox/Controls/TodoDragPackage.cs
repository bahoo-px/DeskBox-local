using DeskBox.ViewModels;

namespace DeskBox.Controls;

/// <summary>
/// Resolves Todo row-drop positions without asking WinUI to mutate the
/// Native AOT object-array projection used by the ListView.
/// </summary>
public static class TodoDragPackage
{
    public static int ResolveManualDropTargetIndex(
        IReadOnlyList<TodoItemViewModel> currentItems,
        string? draggedItemId,
        string? targetItemId,
        bool insertAfter)
    {
        if (string.IsNullOrWhiteSpace(draggedItemId) ||
            string.IsNullOrWhiteSpace(targetItemId))
        {
            return -1;
        }

        int currentIndex = -1;
        int targetIndex = -1;
        for (int index = 0; index < currentItems.Count; index++)
        {
            TodoItemViewModel item = currentItems[index];
            if (string.Equals(item.Id, draggedItemId, StringComparison.Ordinal))
            {
                currentIndex = index;
            }

            if (string.Equals(item.Id, targetItemId, StringComparison.Ordinal))
            {
                targetIndex = index;
            }
        }

        if (currentIndex < 0 || targetIndex < 0)
        {
            return -1;
        }

        int insertionBoundary = targetIndex + (insertAfter ? 1 : 0);
        return insertionBoundary - (currentIndex < insertionBoundary ? 1 : 0);
    }
}
