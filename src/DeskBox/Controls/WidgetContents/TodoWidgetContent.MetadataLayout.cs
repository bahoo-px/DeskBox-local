using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls.WidgetContents;

public sealed partial class TodoWidgetContent
{
    private const double MetadataSingleRowEnterWidth = 520;
    private const double MetadataSingleRowExitWidth = 500;
    private bool _isMetadataSingleRow;

    private void DetailMetadataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool useSingleRow = _isMetadataSingleRow
            ? e.NewSize.Width >= MetadataSingleRowExitWidth
            : e.NewSize.Width >= MetadataSingleRowEnterWidth;
        if (_isMetadataSingleRow == useSingleRow)
        {
            return;
        }

        _isMetadataSingleRow = useSingleRow;
        ApplyDetailMetadataLayout();
    }

    private void ApplyDetailMetadataLayout()
    {
        DetailMetadataColumn0.Width = new GridLength(1, GridUnitType.Star);
        DetailMetadataColumn1.Width = new GridLength(1, GridUnitType.Star);
        DetailMetadataColumn2.Width = _isMetadataSingleRow
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        DetailMetadataColumn3.Width = _isMetadataSingleRow
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        DetailMetadataRow1.Height = _isMetadataSingleRow
            ? new GridLength(0)
            : GridLength.Auto;

        Grid.SetRow(DetailDueDateMetadataButton, 0);
        Grid.SetColumn(DetailDueDateMetadataButton, 0);
        Grid.SetRow(DetailReminderMetadataButton, 0);
        Grid.SetColumn(DetailReminderMetadataButton, 1);
        Grid.SetRow(DetailRecurrenceMetadataButton, _isMetadataSingleRow ? 0 : 1);
        Grid.SetColumn(DetailRecurrenceMetadataButton, _isMetadataSingleRow ? 2 : 0);
        Grid.SetRow(DetailColorMetadataButton, _isMetadataSingleRow ? 0 : 1);
        Grid.SetColumn(DetailColorMetadataButton, _isMetadataSingleRow ? 3 : 1);
    }
}
