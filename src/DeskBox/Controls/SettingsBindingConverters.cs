using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace DeskBox.Controls;

/// <summary>
/// Boolean-to-visibility converter for the settings sections whose editors
/// keep a WinUI-free binding surface (the appearance family's slider gates).
/// </summary>
public sealed partial class SettingsBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts between a <c>#RRGGBB</c> string and a <see cref="Color"/> for the
/// color pickers bound to the appearance editor's hex-string projections.
/// Round-trips preserve the canonical uppercase format the persistence layer
/// stores.
/// </summary>
public sealed partial class SettingsColorStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Color color = TryParse(value as string, out Color parsed)
            ? parsed
            : Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
        return color;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
        {
            return FormattableString.Invariant(
                $"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        if (value is string text && TryParse(text, out Color parsed))
        {
            return FormattableString.Invariant(
                $"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}");
        }

        return "#0078D4";
    }

    private static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string hex = text.Trim().TrimStart('#');
        if (hex.Length != 6 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
        {
            return false;
        }

        color = Color.FromArgb(0xFF, (byte)((packed >> 16) & 0xFF), (byte)((packed >> 8) & 0xFF), (byte)(packed & 0xFF));
        return true;
    }
}
