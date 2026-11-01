using DeskBox.Helpers;
using DeskBox.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DeskBox.Controls;

/// <summary>
/// A single row in the search popup result list. Receives a typed
/// <see cref="SearchResultItem"/> bridge while retaining the inherited DataContext and owns its own hover and
/// keyboard-selection visuals, so the popup window only needs to flip
/// <see cref="IsSelected"/> and call <see cref="RefreshIconVisuals"/> when the
/// lazily resolved shell icon arrives.
/// </summary>
public sealed partial class SearchResultRowControl : UserControl
{
    // Captured from XAML (Transparent) so the row stays hit-testable even when no
    // hover/selection brush is applied.
    private readonly Brush _defaultBackground;
    private bool _isHovered;
    private bool _isPressed;
    private bool _isMultiSelected;

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(SearchResultRowControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public SearchResultRowControl()
    {
        InitializeComponent();
        _defaultBackground = RowRoot.Background;
        PointerEntered += OnRowPointerEntered;
        PointerExited += OnRowPointerExited;
        PointerPressed += OnRowPointerPressed;
        PointerReleased += OnRowPointerReleased;
        PointerCaptureLost += OnRowPointerCaptureLost;
    }

    /// <summary>Keyboard-selection state, driven by the popup window.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Multi-selection state (rubber-band / Ctrl+click), driven by the popup window.</summary>
    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set
        {
            if (_isMultiSelected == value)
            {
                return;
            }

            _isMultiSelected = value;
            RefreshBackground();
            MultiSelectedIcon.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Strongly typed projection of the inherited DataContext. It remains internal so
    /// the XAML type-info generator does not emit an invalid parameterless activator
    /// for SearchResultItem, whose Kind and Title members are required.
    /// </summary>
    internal SearchResultItem? Item { get; private set; }

    /// <summary>
    /// Rebinds all one-time compiled leaves whenever ItemsRepeater prepares or recycles
    /// this element. Calling this even for the same object also preserves the lazy
    /// metadata refresh boundary without making SearchResultItem artificially observable.
    /// </summary>
    internal void PrepareItem(SearchResultItem? item)
    {
        Item = item;
        Bindings.Update();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SearchResultRowControl control)
        {
            return;
        }

        control.SelectionBar.Visibility = (bool)e.NewValue
            ? Visibility.Visible
            : Visibility.Collapsed;
        control.RefreshBackground();
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = true;
        RefreshBackground();
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        RefreshBackground();
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPressed = true;
        RefreshBackground();
    }

    private void OnRowPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = false;
        RefreshBackground();
    }

    private void OnRowPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = false;
        RefreshBackground();
    }

    /// <summary>
    /// Recomputes the row background from the current hover and selection state so the
    /// two never fight over the Background property.
    /// </summary>
    private void RefreshBackground()
    {
        if (IsMultiSelected)
        {
            RowRoot.Background = ResolveThemeBrush(
                _isPressed || _isHovered
                    ? "ControlFillColorTertiaryBrush"
                    : "ControlFillColorSecondaryBrush");
            return;
        }

        if (IsSelected)
        {
            RowRoot.Background = ResolveThemeBrush(
                _isPressed
                    ? "SubtleFillColorTertiaryBrush"
                    : "SubtleFillColorSecondaryBrush");
            return;
        }

        RowRoot.Background = _isPressed
            ? ResolveThemeBrush("SubtleFillColorTertiaryBrush")
            : _isHovered
                ? ResolveThemeBrush("SubtleFillColorSecondaryBrush")
                : _defaultBackground;
    }

    /// <summary>
    /// Syncs the shell icon / glyph fallback toggle and the icon source. Called by the
    /// popup window whenever a row element is (re)prepared: <see cref="SearchResultItem.Icon"/>
    /// is populated lazily by the FileMetaService and is not observable, and recycled
    /// rows may be re-bound to the same item instance (no DataContextChanged), so the
    /// XAML binding alone cannot track it.
    /// </summary>
    public void RefreshIconVisuals()
    {
        var item = Item;
        bool hasIcon = item?.Icon is not null;
        FileIcon.Source = item?.Icon;
        FileIcon.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;
        GlyphBlock.Visibility = hasIcon ? Visibility.Collapsed : Visibility.Visible;
        SizeText.Text = item?.SizeDisplay;
        DateText.Text = item?.DateDisplay;
    }

    public void SetFileColumnsVisible(bool visible)
    {
        TypeColumn.Width = visible ? new GridLength(75) : new GridLength(0);
        SizeColumn.Width = visible ? new GridLength(90) : new GridLength(0);
        DateColumn.Width = visible ? new GridLength(110) : new GridLength(0);
        TypeText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SizeText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        DateText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private Brush? ResolveThemeBrush(string key) =>
        NeutralInteractionBrush.ResolveThemedResource(key, this);
}
