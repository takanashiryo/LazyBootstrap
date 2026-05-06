using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LazyBootstrap.Services.Environment;

namespace LazyBootstrap.Converters
{
    /// <summary>
    /// Maps <see cref="EnvironmentScan.ScanResultLevel"/> + role string (Text, Border, RowFill, BadgeBg, BadgeFg) to a brush.
    /// </summary>
    public sealed class EnvironmentScanAccentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not EnvironmentScan.ScanResultLevel level)
            {
                return Brushes.Transparent;
            }

            string role = parameter as string ?? "Text";
            Color accent = LevelToRgb(level);

            return role switch
            {
                "Text" => new SolidColorBrush(Color.FromArgb(255, accent.R, accent.G, accent.B)),
                "Fg" => new SolidColorBrush(Color.FromArgb(255, accent.R, accent.G, accent.B)),
                "Border" => new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)),
                "RowFill" => new SolidColorBrush(Color.FromArgb(24, accent.R, accent.G, accent.B)),
                "RowStroke" => new SolidColorBrush(Color.FromArgb(96, accent.R, accent.G, accent.B)),
                "BadgeBg" => new SolidColorBrush(Color.FromArgb(GetBadgeBgAlpha(level), accent.R, accent.G, accent.B)),
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static byte GetBadgeBgAlpha(EnvironmentScan.ScanResultLevel l)
        {
            return l switch
            {
                EnvironmentScan.ScanResultLevel.Success => 28,
                EnvironmentScan.ScanResultLevel.Warning => 26,
                _ => 30
            };
        }

        private static Color LevelToRgb(EnvironmentScan.ScanResultLevel level)
        {
            return level switch
            {
                EnvironmentScan.ScanResultLevel.Success => Color.FromRgb(82, 196, 26),
                EnvironmentScan.ScanResultLevel.Warning => Color.FromRgb(250, 140, 22),
                _ => Color.FromRgb(245, 34, 45)
            };
        }
    }
}
