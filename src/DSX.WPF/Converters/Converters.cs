using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSX.Core.Enums;

namespace DSX.WPF.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v != Visibility.Visible;
        return true;
    }
}

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var paramStr = parameter.ToString()!;
        var targetValues = paramStr.Contains('|') ? paramStr.Split('|') : paramStr.Split(',');

        foreach (var target in targetValues)
        {
            if (string.Equals(enumValue, target.Trim(), StringComparison.OrdinalIgnoreCase))
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color color)
            return new SolidColorBrush(color);
        if (value is string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(c);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return brush.Color;
        return Colors.Transparent;
    }
}

public class BatteryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percentage = 0;

        if (value is double d)
            percentage = d;
        else if (value is int i)
            percentage = i;
        else if (value is float f)
            percentage = f;

        return percentage switch
        {
            >= 60 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA")),
            >= 30 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB800")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3333"))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ConnectionTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionType ct)
        {
            return ct switch
            {
                ConnectionType.USB => "\uE85E",
                ConnectionType.Bluetooth => "\uE702",
                ConnectionType.USBWirelessAdapter => "\uE85E",
                _ => "\uE968"
            };
        }
        return "\uE968";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ConnectionStateToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionType ct)
        {
            return ct switch
            {
                ConnectionType.USB => "USB Connected",
                ConnectionType.Bluetooth => "Bluetooth Connected",
                ConnectionType.USBWirelessAdapter => "Wireless Adapter Connected",
                _ => "Unknown Connection"
            };
        }
        return "Disconnected";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage && parameter is string totalWidthStr
            && double.TryParse(totalWidthStr, out double totalWidth))
        {
            return Math.Max(0, Math.Min(totalWidth, (percentage / 100.0) * totalWidth));
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var isNull = value == null;

        if (value is string str)
            isNull = string.IsNullOrEmpty(str);

        return invert
            ? (isNull ? Visibility.Visible : Visibility.Collapsed)
            : (isNull ? Visibility.Collapsed : Visibility.Visible);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var field = value.GetType().GetField(value.ToString()!);
        if (field != null)
        {
            var description = (System.ComponentModel.DescriptionAttribute[]?)
                field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            if (description is { Length: > 0 })
                return description[0].Description;
        }

        return value.ToString()!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && targetType.IsEnum)
        {
            foreach (var field in targetType.GetFields())
            {
                var description = (System.ComponentModel.DescriptionAttribute[]?)
                    field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                if (description is { Length: > 0 } && description[0].Description == str)
                    return Enum.Parse(targetType, field.Name);
            }
            return Enum.Parse(targetType, str);
        }
        return value;
    }
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var hasItems = value is int count && count > 0;
        return invert
            ? (hasItems ? Visibility.Collapsed : Visibility.Visible)
            : (hasItems ? Visibility.Visible : Visibility.Collapsed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DoubleToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{d:0}%";
        if (value is int i)
            return $"{i}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && double.TryParse(s.TrimEnd('%'), out double result))
            return result;
        return 0.0;
    }
}

public class RgbToBrushConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        byte r = 0, g = 0, b = 0;
        if (values.Length > 0 && values[0] is byte rv) r = rv;
        else if (values.Length > 0 && values[0] is int ri) r = (byte)ri;
        if (values.Length > 1 && values[1] is byte gv) g = gv;
        else if (values.Length > 1 && values[1] is int gi) g = (byte)gi;
        if (values.Length > 2 && values[2] is byte bv) b = bv;
        else if (values.Length > 2 && values[2] is int bi) b = (byte)bi;
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LanguageToFlagConverter : IMultiValueConverter
{
    private static readonly Dictionary<string, string> LanguageToFlagFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar-SA"] = "ar", ["bg-BG"] = "bg", ["da-DK"] = "da", ["de-DE"] = "de",
        ["el-GR"] = "en-GB", ["en-GB"] = "en-GB", ["en-US"] = "en-US-Flag",
        ["es-419"] = "es", ["fi-FI"] = "fi", ["fr-FR"] = "fr", ["hr-HR"] = "hr",
        ["hu-HU"] = "hu", ["id-ID"] = "id", ["it-IT"] = "it", ["ja-JP"] = "en-GB",
        ["ko-KR"] = "ko", ["nb-NO"] = "nb", ["nl-NL"] = "nl", ["pl-PL"] = "pl",
        ["pt-BR"] = "pt-BR", ["pt-PT"] = "en-GB", ["ro-RO"] = "ro", ["ru-RU"] = "ru",
        ["sv-SE"] = "sv", ["th-TH"] = "th", ["tr-TR"] = "tr", ["uk-UA"] = "uk",
        ["vi-VN"] = "en-GB", ["zh-CN"] = "zh-Hans", ["zh-TW"] = "en-GB"
    };

    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is string lang && LanguageToFlagFile.TryGetValue(lang, out var flag))
        {
            return new BitmapImage(new Uri($"pack://application:,,,/DSX;component/Resources/Images/Flags/{flag}.png"));
        }
        return null;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
