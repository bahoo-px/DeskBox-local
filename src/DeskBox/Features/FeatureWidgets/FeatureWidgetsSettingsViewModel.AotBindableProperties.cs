#if DESKBOX_NATIVE_AOT
namespace DeskBox.Features.FeatureWidgets;

// The General section's attachment-storage combo resolves its {Binding}
// surface against this editor through an element-level DataContext (batch
// 50); the file-widget overview reaches the folder-open surface through a
// typed x:Bind dependency property, which needs no bridge. Expose only the
// properties used by runtime {Binding} markup in NativeAOT builds,
// mirroring the GlanceWidgetViewModel bridge pattern.
[WinRT.GeneratedBindableCustomProperty([
    nameof(AttachmentStorageMode),
    nameof(AvailableAttachmentStorageModeOptions)
], [])]
public sealed partial class FeatureWidgetsSettingsViewModel
{
}
#endif
