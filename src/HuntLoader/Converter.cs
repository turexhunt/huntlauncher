// src/HuntLoader/Converter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HuntLoader.ViewModels;

namespace HuntLoader;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? (object)!b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? (object)!b : false;
}

public class LineTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LineType type)
        {
            return type switch
            {
                LineType.Error   => new SolidColorBrush(Color.FromRgb(244, 67,  54)),
                LineType.Warning => new SolidColorBrush(Color.FromRgb(255, 152,  0)),
                LineType.System  => new SolidColorBrush(Color.FromRgb(255, 107, 53)),
                _                => new SolidColorBrush(Color.FromRgb(200, 200, 220))
            };
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class BoolToEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "✅" : "⛔";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// 0..100 → 0..maxWidth (для прогресс-баров)
/// ConverterParameter = максимальная ширина в пикселях
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double percent) return 0.0;
        if (parameter is not string paramStr) return 0.0;
        if (!double.TryParse(paramStr, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var maxWidth)) return 0.0;
        return maxWidth * (Math.Clamp(percent, 0, 100) / 100.0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Сравнивает value.ToString() с ConverterParameter.
/// Используется для привязки RadioButton к строковому свойству.
/// </summary>
public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
        => value is true
            ? parameter?.ToString() ?? ""
            : Binding.DoNothing;
}

/// <summary>
/// null → Collapsed, not null → Visible
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// string пустая/null → Collapsed
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value?.ToString())
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// int/double 0 → Collapsed, >0 → Visible
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            int    i => i > 0 ? Visibility.Visible : Visibility.Collapsed,
            double d => d > 0 ? Visibility.Visible : Visibility.Collapsed,
            long   l => l > 0 ? Visibility.Visible : Visibility.Collapsed,
            _        => Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Hex строка цвета "#RRGGBB" → SolidColorBrush
/// Используется для Source бейджей модов
/// </summary>
public class StringToColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrEmpty(hex))
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
        }
        catch { /* ignore */ }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// ModLoader enum → красивое имя
/// </summary>
public class ModLoaderToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.ModLoader loader)
        {
            return loader switch
            {
                Models.ModLoader.Fabric   => "🟢 Fabric",
                Models.ModLoader.Forge    => "🔥 Forge",
                Models.ModLoader.Quilt    => "🧵 Quilt",
                Models.ModLoader.NeoForge => "⚡ NeoForge",
                _                         => "🎮 Vanilla"
            };
        }
        return "Vanilla";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// AccountType enum → иконка
/// </summary>
public class AccountTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.AccountType type)
        {
            return type switch
            {
                Models.AccountType.Microsoft => "⭐",
                Models.AccountType.ElyBy     => "🔵",
                _                            => "👤"
            };
        }
        return "👤";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// bool isActive → цвет фона карточки аккаунта
/// </summary>
public class ActiveAccountBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
            return new SolidColorBrush(Color.FromArgb(40, 139, 111, 212));
        return new SolidColorBrush(Color.FromArgb(255, 22, 22, 38));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// TimeSpan → красивая строка "2ч 30м" или "45м"
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}м";
            return "< 1м";
        }
        return "0м";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// DateTime → относительное время "2 часа назад", "вчера" и т.д.
/// </summary>
public class DateTimeToRelativeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return "";
        if (dt == DateTime.MinValue)  return "Никогда";

        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 1)  return "Только что";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} мин. назад";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours} ч. назад";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays} дн. назад";
        return dt.ToString("dd.MM.yyyy");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// double MB память → строка "2048 MB"
/// </summary>
public class MemoryToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int mb)    return $"{mb} MB";
        if (value is double d)  return $"{(int)d} MB";
        return "0 MB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            var num = s.Replace(" MB", "").Replace("MB", "").Trim();
            if (int.TryParse(num, out var result)) return result;
        }
        return 0;
    }
}

// ✅ НОВЫЙ: HEX строка → Color для биндинга в XAML
/// <summary>
/// "#RRGGBB" → Color (для использования в SolidColorBrush binding)
/// Пример: Color="{Binding AccentColor, Converter={StaticResource HexToColor}}"
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrEmpty(hex))
                return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch { /* ignore */ }
        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Color color ? color.ToString() : "#7C5CBF";
}