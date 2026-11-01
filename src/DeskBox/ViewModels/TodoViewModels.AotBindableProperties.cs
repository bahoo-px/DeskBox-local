#if DESKBOX_NATIVE_AOT
namespace DeskBox.ViewModels;

// TodoWidgetContent intentionally keeps its existing runtime Binding surface.
// NativeAOT therefore needs generated custom-property providers for the three
// data-context types exercised by the core task/detail and step matrices.
[WinRT.GeneratedBindableCustomProperty]
public partial class TodoWidgetViewModel
{
}

[WinRT.GeneratedBindableCustomProperty]
public partial class TodoItemViewModel
{
}

[WinRT.GeneratedBindableCustomProperty]
public partial class TodoStepViewModel
{
}
#endif
