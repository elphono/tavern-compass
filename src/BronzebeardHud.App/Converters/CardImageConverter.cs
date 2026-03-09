using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using BronzebeardHud.App.Services;

namespace BronzebeardHud.App.Converters;

public class CardImageConverter : IValueConverter
{
    public static readonly CardImageConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string cardId && !string.IsNullOrEmpty(cardId))
            return CardImageCache.Instance.Get(cardId);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
