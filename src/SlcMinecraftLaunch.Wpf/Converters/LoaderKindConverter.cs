using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.Converters;

public sealed class LoaderKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LoaderKind kind)
        {
            return string.Empty;
        }

        var key = $"Loader.{kind}";
        return System.Windows.Application.Current.TryFindResource(key)?.ToString() ?? kind.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

