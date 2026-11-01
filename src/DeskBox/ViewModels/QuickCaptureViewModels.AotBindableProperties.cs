#if DESKBOX_NATIVE_AOT
namespace DeskBox.ViewModels;

// The window and embedded Quick Capture surfaces deliberately share runtime
// Binding markup. Preserve both the root controls and item-template values
// when Native AOT removes reflection metadata.
[WinRT.GeneratedBindableCustomProperty]
public sealed partial class QuickCaptureWidgetViewModel
{
}

[WinRT.GeneratedBindableCustomProperty]
public sealed partial class QuickCaptureItemViewModel
{
}
#endif
