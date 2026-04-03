using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AirLiticApp;

/// <summary>
/// Ширина сегмента горизонтальної смуги: (відсоток 0–100) × (ширина контейнера мінус підписи/KPI).
/// Друга прив'язка — зазвичай ActualWidth ScrollViewer з графіком.
/// </summary>
public class ChartTrackSegmentWidthConverter : IMultiValueConverter
{
    /// <summary>Віднімається від ширини контейнера: колонка підпису, KPI, відступи.</summary>
    public double ChromeReserve { get; set; } = 215;

    /// <summary>Мінімальна ширина «рейки» для масштабування (px).</summary>
    public double MinTrackWidth { get; set; } = 48;

    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return 0d;

        var pct = ToDouble(values[0]);
        var containerW = ToDouble(values[1]);
        if (containerW <= 0 || double.IsNaN(containerW) || double.IsInfinity(containerW))
            return 0d;

        var track = containerW - ChromeReserve;
        if (track < MinTrackWidth)
            track = MinTrackWidth;

        if (pct <= 0)
            return 0d;

        var w = pct / 100.0 * track;
        return w < 0 ? 0d : w;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ToDouble(object? o)
    {
        if (o == null || o == DependencyProperty.UnsetValue)
            return 0d;
        return o switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => System.Convert.ToDouble(o, CultureInfo.InvariantCulture)
        };
    }
}
