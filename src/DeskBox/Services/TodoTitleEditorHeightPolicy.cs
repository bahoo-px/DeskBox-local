namespace DeskBox.Services;

public static class TodoTitleEditorHeightPolicy
{
    public const double MinimumHeight = 56;
    public const double EmptyHeight = 76;
    public const double AbsoluteMaximumHeight = 240;
    public const double DefaultAvailableHeight = 420;
    private const double AvailableHeightRatio = 0.34;

    public static double ResolveMaximumHeight(double availableHeight)
    {
        double normalizedAvailableHeight = double.IsFinite(availableHeight) && availableHeight > 0
            ? availableHeight
            : DefaultAvailableHeight;
        return Math.Clamp(
            normalizedAvailableHeight * AvailableHeightRatio,
            EmptyHeight,
            AbsoluteMaximumHeight);
    }

    public static double ResolveHeight(
        double measuredContentHeight,
        double availableHeight,
        bool isEmpty,
        double? preferredHeight = null)
    {
        double maximumHeight = ResolveMaximumHeight(availableHeight);
        if (preferredHeight is double preferred && double.IsFinite(preferred))
        {
            return Math.Clamp(preferred, MinimumHeight, maximumHeight);
        }

        double contentHeight = double.IsFinite(measuredContentHeight)
            ? measuredContentHeight
            : MinimumHeight;
        double target = isEmpty
            ? EmptyHeight
            : Math.Max(MinimumHeight, contentHeight);
        return Math.Clamp(target, MinimumHeight, maximumHeight);
    }

    public static double NormalizePersistedHeight(double height) =>
        double.IsFinite(height)
            ? Math.Clamp(height, MinimumHeight, AbsoluteMaximumHeight)
            : EmptyHeight;
}
