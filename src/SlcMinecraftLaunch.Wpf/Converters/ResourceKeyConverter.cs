using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlcMinecraftLaunch.Wpf.Converters;

public sealed class ResourceKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value?.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return System.Windows.Application.Current.TryFindResource(key)?.ToString()
               ?? key;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

