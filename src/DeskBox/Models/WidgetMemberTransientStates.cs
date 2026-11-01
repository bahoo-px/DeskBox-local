namespace DeskBox.Models;

public sealed record QuickCaptureWidgetTransientState(
    string InputText,
    string SearchText,
    QuickCaptureViewMode SelectedView,
    string FocusTarget,
    string? SelectedDetailItemId = null,
    bool IsDetailEditing = false,
    string? DetailDraft = null,
    bool WasDetailVisibleInSinglePane = false);

public sealed record FileWidgetTransientState(
    string[] SelectedPaths,
    string[] CutPaths);
