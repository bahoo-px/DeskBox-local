using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls.WidgetContents;

public enum MusicTransportIconKind
{
    Previous,
    Play,
    Pause,
    Next
}

public sealed partial class MusicTransportIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(MusicTransportIconKind),
        typeof(MusicTransportIcon),
        new PropertyMetadata(MusicTransportIconKind.Play, OnKindChanged));

    public MusicTransportIcon()
    {
        InitializeComponent();
        ApplyKind();
    }

    public MusicTransportIconKind Kind
    {
        get => (MusicTransportIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnKindChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is MusicTransportIcon icon)
        {
            icon.ApplyKind();
        }
    }

    private void ApplyKind()
    {
        PreviousSurface.Visibility = Kind == MusicTransportIconKind.Previous
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlaySurface.Visibility = Kind == MusicTransportIconKind.Play
            ? Visibility.Visible
            : Visibility.Collapsed;
        PauseSurface.Visibility = Kind == MusicTransportIconKind.Pause
            ? Visibility.Visible
            : Visibility.Collapsed;
        NextSurface.Visibility = Kind == MusicTransportIconKind.Next
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
