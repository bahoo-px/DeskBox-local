namespace DeskBox.Controls;

public enum MasterDetailLayoutMode
{
    SinglePane,
    DualPane
}

public enum MasterDetailLayoutPreference
{
    Auto,
    SinglePane,
    DualPane
}

public sealed record MasterDetailLayoutOptions(
    double DualPaneEnterWidth = 720,
    double DualPaneExitWidth = 680,
    double DefaultMasterWidth = 300,
    double MinimumMasterWidth = 240,
    double MaximumMasterWidth = 420,
    double MinimumDetailWidth = 340,
    double SplitterWidth = 8)
{
    public void Validate()
    {
        if (!double.IsFinite(DualPaneEnterWidth) || !double.IsFinite(DualPaneExitWidth) ||
            !double.IsFinite(DefaultMasterWidth) || !double.IsFinite(MinimumMasterWidth) ||
            !double.IsFinite(MaximumMasterWidth) || !double.IsFinite(MinimumDetailWidth) ||
            !double.IsFinite(SplitterWidth) ||
            DualPaneEnterWidth <= 0 || DualPaneExitWidth <= 0 ||
            DualPaneExitWidth > DualPaneEnterWidth || MinimumMasterWidth <= 0 ||
            MaximumMasterWidth < MinimumMasterWidth ||
            DefaultMasterWidth < MinimumMasterWidth || DefaultMasterWidth > MaximumMasterWidth ||
            MinimumDetailWidth <= 0 || SplitterWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MasterDetailLayoutOptions));
        }
    }
}

public sealed record MasterDetailLayoutSnapshot(
    MasterDetailLayoutMode Mode,
    double MasterWidth,
    double SplitterWidth,
    double DetailWidth)
{
    public bool IsDualPane => Mode == MasterDetailLayoutMode.DualPane;
}

/// <summary>
/// Pure, host-independent responsive policy shared by Todo and Quick Capture.
/// It applies hysteresis, protects both panes, and normalizes persisted widths.
/// </summary>
public sealed class MasterDetailLayoutPolicy
{
    public MasterDetailLayoutPolicy(MasterDetailLayoutOptions? options = null)
    {
        Options = options ?? new MasterDetailLayoutOptions();
        Options.Validate();
    }

    public MasterDetailLayoutOptions Options { get; }

    public MasterDetailLayoutSnapshot Resolve(
        double availableWidth,
        bool wasDualPane,
        double? requestedMasterWidth = null,
        bool forceSinglePane = false,
        bool forceDualPane = false)
    {
        MasterDetailLayoutPreference preference = forceSinglePane
            ? MasterDetailLayoutPreference.SinglePane
            : forceDualPane
                ? MasterDetailLayoutPreference.DualPane
                : MasterDetailLayoutPreference.Auto;
        return Resolve(availableWidth, wasDualPane, requestedMasterWidth, preference);
    }

    public MasterDetailLayoutSnapshot Resolve(
        double availableWidth,
        bool wasDualPane,
        double? requestedMasterWidth,
        MasterDetailLayoutPreference preference)
    {
        double width = double.IsFinite(availableWidth)
            ? Math.Max(0, availableWidth)
            : 0;
        double minimumDualWidth = Options.MinimumMasterWidth +
                                  Options.SplitterWidth +
                                  Options.MinimumDetailWidth;
        double automaticThreshold = wasDualPane
            ? Math.Max(Options.DualPaneExitWidth, minimumDualWidth)
            : Math.Max(Options.DualPaneEnterWidth, minimumDualWidth);
        bool dualPane = preference switch
        {
            MasterDetailLayoutPreference.SinglePane => false,
            MasterDetailLayoutPreference.DualPane => true,
            _ => width >= automaticThreshold
        };
        if (!dualPane)
        {
            return new MasterDetailLayoutSnapshot(
                MasterDetailLayoutMode.SinglePane,
                width,
                0,
                0);
        }

        if (preference == MasterDetailLayoutPreference.DualPane && width < minimumDualWidth)
        {
            double compressedSplitterWidth = Math.Min(width, Options.SplitterWidth);
            double paneWidth = Math.Max(0, width - compressedSplitterWidth);
            double compressedDesiredMasterWidth = requestedMasterWidth is { } compressedRequested &&
                                                  double.IsFinite(compressedRequested)
                ? NormalizePersistedMasterWidth(compressedRequested)
                : Options.DefaultMasterWidth;
            double masterRatio = compressedDesiredMasterWidth /
                                 (compressedDesiredMasterWidth + Options.MinimumDetailWidth);
            double compressedMasterWidth = paneWidth * masterRatio;
            return new MasterDetailLayoutSnapshot(
                MasterDetailLayoutMode.DualPane,
                compressedMasterWidth,
                compressedSplitterWidth,
                Math.Max(0, paneWidth - compressedMasterWidth));
        }

        double maximumMasterWidth = Math.Clamp(
            width - Options.SplitterWidth - Options.MinimumDetailWidth,
            Options.MinimumMasterWidth,
            Options.MaximumMasterWidth);
        double desiredMasterWidth = requestedMasterWidth is { } requested && double.IsFinite(requested)
            ? requested
            : Options.DefaultMasterWidth;
        double masterWidth = Math.Clamp(
            desiredMasterWidth,
            Options.MinimumMasterWidth,
            maximumMasterWidth);
        return new MasterDetailLayoutSnapshot(
            MasterDetailLayoutMode.DualPane,
            masterWidth,
            Options.SplitterWidth,
            Math.Max(0, width - masterWidth - Options.SplitterWidth));
    }

    public double NormalizePersistedMasterWidth(double? value)
    {
        if (value is not { } width || !double.IsFinite(width))
        {
            return Options.DefaultMasterWidth;
        }

        return Math.Clamp(width, Options.MinimumMasterWidth, Options.MaximumMasterWidth);
    }
}
