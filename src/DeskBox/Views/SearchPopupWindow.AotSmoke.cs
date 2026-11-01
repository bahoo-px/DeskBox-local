#if DESKBOX_NATIVE_AOT
using DeskBox.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace DeskBox.Views;

public sealed partial class SearchPopupWindow
{
    internal AotSearchWindowSnapshot CaptureAotSmokeSnapshot()
    {
        return new AotSearchWindowSnapshot(
            WindowNative.GetWindowHandle(this).ToInt64(),
            _appWindow?.IsVisible == true,
            IsPopupVisible,
            RootGrid.XamlRoot is not null,
            SearchTextBox.Text,
            _viewModel.Query,
            _viewModel.IsSearching,
            _viewModel.HasResults,
            _viewModel.HasCurrentResults,
            _viewModel.CurrentResults.Count,
            _viewModel.SelectedTab?.Id,
            _viewModel.ResultFilter.ToString(),
            _viewModel.SortColumn.ToString(),
            _viewModel.SortAscending,
            ResultFilterBar.Visibility == Visibility.Visible,
            SortHeaderRow.Visibility == Visibility.Visible,
            _viewModel.CurrentResults.Any(item => item.ActionId == "open-settings"));
    }

    internal AotSearchControlExercise ExerciseAotReadOnlyControls()
    {
        var filterItems = new (string Name, ComboBoxItem Item)[]
        {
            ("All", FilterAllItem),
            ("FilesAndFolders", FilterFilesItem),
            ("Apps", FilterAppsItem),
            ("Images", FilterImagesItem),
            ("Documents", FilterDocumentsItem),
            ("DeskBox", FilterDeskBoxItem)
        };
        var filterTransitions = new List<string>(filterItems.Length);
        foreach ((string name, ComboBoxItem item) in filterItems)
        {
            ResultFilterComboBox.SelectedItem = item;
            filterTransitions.Add($"{name}:{_viewModel.ResultFilter}");
        }

        ResultFilterComboBox.SelectedItem = FilterAllItem;
        _viewModel.SortColumn = ResultSortColumn.Relevance;
        _viewModel.SortAscending = true;

        var sortTransitions = new List<string>(8);
        SortNameHeader_Click(SortNameHeader, null!);
        sortTransitions.Add($"Name:{_viewModel.SortAscending}");
        SortNameHeader_Click(SortNameHeader, null!);
        sortTransitions.Add($"Name:{_viewModel.SortAscending}");

        SortSizeHeader_Click(SortSizeHeader, null!);
        sortTransitions.Add($"Size:{_viewModel.SortAscending}");
        SortSizeHeader_Click(SortSizeHeader, null!);
        sortTransitions.Add($"Size:{_viewModel.SortAscending}");

        SortDateHeader_Click(SortDateHeader, null!);
        sortTransitions.Add($"Date:{_viewModel.SortAscending}");
        SortDateHeader_Click(SortDateHeader, null!);
        sortTransitions.Add($"Date:{_viewModel.SortAscending}");

        SortTypeHeader_Click(SortTypeHeader, null!);
        sortTransitions.Add($"Type:{_viewModel.SortAscending}");
        SortTypeHeader_Click(SortTypeHeader, null!);
        sortTransitions.Add($"Type:{_viewModel.SortAscending}");

        ResultFilterComboBox.SelectedItem = FilterAllItem;
        _viewModel.SortColumn = ResultSortColumn.Relevance;
        _viewModel.SortAscending = true;
        UpdateSortHeaders();

        return new AotSearchControlExercise(filterTransitions, sortTransitions);
    }
}

internal sealed record AotSearchWindowSnapshot(
    long WindowHandle,
    bool IsAppWindowVisible,
    bool IsPopupVisible,
    bool HasXamlRoot,
    string TextBoxQuery,
    string ViewModelQuery,
    bool IsSearching,
    bool HasResults,
    bool HasCurrentResults,
    int CurrentResultsCount,
    string? SelectedTabId,
    string ResultFilter,
    string SortColumn,
    bool SortAscending,
    bool IsResultFilterBarVisible,
    bool IsSortHeaderRowVisible,
    bool HasOpenSettingsAction);

internal sealed record AotSearchControlExercise(
    IReadOnlyList<string> FilterTransitions,
    IReadOnlyList<string> SortTransitions);
#endif
