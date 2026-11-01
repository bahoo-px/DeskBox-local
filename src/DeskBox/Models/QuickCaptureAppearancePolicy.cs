namespace DeskBox.Models;

/// <summary>
/// Shared appearance rules for Quick Capture list surfaces. Clipboard/recent
/// entries are read-only captures and never expose a paper material, even if
/// a recycled view temporarily carries a record item's previous state.
/// </summary>
public static class QuickCaptureAppearancePolicy
{
    public static QuickCaptureAppearancePreset ResolveListPreset(
        QuickCaptureAppearancePreset appearancePreset,
        bool isRecent)
    {
        return isRecent
            ? QuickCaptureAppearancePreset.Default
            : appearancePreset;
    }
}
