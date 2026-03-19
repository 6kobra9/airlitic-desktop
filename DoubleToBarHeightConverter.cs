using System;
using System.Globalization;
using System.Windows.Data;

namespace AirLiticApp;

public class DoubleToBarHeightConverter : IValueConverter
{
    public double MaxHeight { get; set; } = 180;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return 0d;

        double v;
        try
        {
            v = value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                short s => s,
                byte b => b,
                _ => System.Convert.ToDouble(value, CultureInfo.InvariantCulture)
            };
        }
        catch
        {
            return 0d;
        }

        if (v <= 0)
            return 0d;

        // Ожидаем KPI в диапазоне 0–100 (%)
        var scaled = v / 100.0 * MaxHeight;
        if (scaled > MaxHeight)
            scaled = MaxHeight;

        return scaled;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
