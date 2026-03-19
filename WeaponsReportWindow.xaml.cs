using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AirLiticApp;

public partial class WeaponsReportWindow : Window
{
    public ObservableCollection<BarItem> MainChartItems { get; } = new();
    public ObservableCollection<BarItem> LostChartItems { get; } = new();

    private ChartType _mainChartType = ChartType.Bar;
    private ChartType _lostChartType = ChartType.Bar;

    public WeaponsReportWindow(DataTable mainReport, DataTable? lostReport = null)
    {
        InitializeComponent();
        DataContext = this;

        ReportGrid.ItemsSource = mainReport.DefaultView;
        BuildMainChart(mainReport);
        _mainChartType = FromComboIndex(MainChartTypeCombo?.SelectedIndex ?? 0);
        try
        {
            RenderMainChart();
        }
        catch
        {
            // В случае проблем с графиком — не ломаем весь отчет.
            // Таблица должна остаться видимой.
        }

        if (lostReport != null)
        {
            LostReportGrid.ItemsSource = lostReport.DefaultView;
            BuildLostChart(lostReport);
            _lostChartType = FromComboIndex(LostChartTypeCombo.SelectedIndex);
            RenderLostChart();
        }
        else
        {
            LostReportGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Первый таб: таблицы, второй таб: графики
        if (MainTabControl?.SelectedIndex == 1)
        {
            try { RenderMainChart(); } catch { }
            try { RenderLostChart(); } catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BuildMainChart(DataTable table)
    {
        MainChartItems.Clear();
        if (!table.Columns.Contains("KPI"))
            return;

        foreach (DataRow row in table.Rows)
        {
            var label = row[0]?.ToString() ?? string.Empty;
            if (TryToDouble(row["KPI"], out var value))
                MainChartItems.Add(new BarItem { Label = label, Value = value });
        }
    }

    private void BuildLostChart(DataTable table)
    {
        LostChartItems.Clear();

        // Показываем ВСЕ KPI-категории на одном графике.
        // Считаем "общий" KPI как взвешенное среднее по TotalHits:
        // KPI_total = sum(KPI_i * TotalHits_i) / sum(TotalHits_i)

        const string totalHitsColumn = "TotalHits";
        if (!table.Columns.Contains(totalHitsColumn))
            return;

        var totalHitsSum = 0d;

        // KPI колонок из вашего SQL (заголовки должны совпадать 1-в-1).
        var kpis = new (string Label, string Column)[]
        {
            ("Помилка пілота", "KPI помилка пілота"),
            ("Технічні помилки", "KPI технічні помилки"),
            ("Вороже збиття", "KPI вороже збиття"),
            ("Реб свій", "KPI реб свій"),
            ("Реб противника", "KPI реб противника"),
            ("Погодні умови", "KPI погодні умови"),
        };

        var weightedSums = new System.Collections.Generic.Dictionary<string, double>();
        foreach (var k in kpis)
            weightedSums[k.Column] = 0d;

        foreach (DataRow row in table.Rows)
        {
            if (!TryToDouble(row[totalHitsColumn], out var hits) || hits <= 0)
                continue;

            totalHitsSum += hits;

            foreach (var k in kpis)
            {
                if (!table.Columns.Contains(k.Column))
                    continue;

                if (TryToDouble(row[k.Column], out var kpiValue))
                    weightedSums[k.Column] += kpiValue * hits;
            }
        }

        if (totalHitsSum <= 0)
            totalHitsSum = 0d;

        foreach (var k in kpis)
        {
            if (!table.Columns.Contains(k.Column))
                continue;

            var v = totalHitsSum <= 0 ? 0d : weightedSums[k.Column] / totalHitsSum;
            LostChartItems.Add(new BarItem { Label = k.Label, Value = v });
        }
    }

    private static bool TryToDouble(object? value, out double result)
    {
        result = 0;
        if (value == null || value == DBNull.Value)
            return false;

        try
        {
            result = value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                short s => s,
                byte b => b,
                _ => Convert.ToDouble(value)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private enum ChartType
    {
        Bar = 0,
        Line = 1,
        Pie = 2
    }

    private static ChartType FromComboIndex(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => ChartType.Line,
            2 => ChartType.Pie,
            _ => ChartType.Bar,
        };
    }

    private void MainChartTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _mainChartType = FromComboIndex(MainChartTypeCombo?.SelectedIndex ?? 0);
        try
        {
            RenderMainChart();
        }
        catch
        {
        }
    }

    private void LostChartTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _lostChartType = FromComboIndex(LostChartTypeCombo?.SelectedIndex ?? 0);
        try
        {
            RenderLostChart();
        }
        catch
        {
        }
    }

    private void RenderMainChart()
    {
        if (MainBarChartPanel == null || MainLineCanvas == null || MainPieCanvas == null)
            return;

        if (_mainChartType == ChartType.Bar)
        {
            MainBarChartPanel.Visibility = Visibility.Visible;
            MainLineCanvas.Visibility = Visibility.Collapsed;
            MainPieCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        MainBarChartPanel.Visibility = Visibility.Collapsed;
        MainLineCanvas.Visibility = _mainChartType == ChartType.Line ? Visibility.Visible : Visibility.Collapsed;
        MainPieCanvas.Visibility = _mainChartType == ChartType.Pie ? Visibility.Visible : Visibility.Collapsed;

        if (_mainChartType == ChartType.Line)
            DrawLineChart(MainLineCanvas, MainChartItems, Brushes.SteelBlue);
        else
            DrawPieChart(MainPieCanvas, MainChartItems);
    }

    private void RenderLostChart()
    {
        if (LostBarChartPanel == null || LostLineCanvas == null || LostPieCanvas == null)
            return;

        if (_lostChartType == ChartType.Bar)
        {
            LostBarChartPanel.Visibility = Visibility.Visible;
            LostLineCanvas.Visibility = Visibility.Collapsed;
            LostPieCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        LostBarChartPanel.Visibility = Visibility.Collapsed;
        LostLineCanvas.Visibility = _lostChartType == ChartType.Line ? Visibility.Visible : Visibility.Collapsed;
        LostPieCanvas.Visibility = _lostChartType == ChartType.Pie ? Visibility.Visible : Visibility.Collapsed;

        if (_lostChartType == ChartType.Line)
            DrawLineChart(LostLineCanvas, LostChartItems, Brushes.DarkOrange);
        else
            DrawPieChart(LostPieCanvas, LostChartItems);
    }

    private static void DrawLineChart(Canvas canvas, ObservableCollection<BarItem> items, Brush lineBrush)
    {
        canvas.Children.Clear();

        if (items.Count == 0)
            return;

        var width = canvas.ActualWidth;
        if (double.IsNaN(width) || width <= 0)
            width = 720;

        var height = canvas.Height;
        if (double.IsNaN(height) || height <= 0)
            height = 190;

        const double padding = 34;
        var plotWidth = Math.Max(10, width - padding * 2);
        var plotHeight = Math.Max(10, height - padding * 2 - 10);

        var max = 0d;
        foreach (var it in items)
            if (it.Value > max) max = it.Value;
        if (max <= 0) max = 1d;

        var points = new PointCollection();
        for (int i = 0; i < items.Count; i++)
        {
            var x = padding;
            if (items.Count > 1)
                x += i * (plotWidth / (items.Count - 1));
            else
                x += plotWidth / 2;

            var y = padding + plotHeight * (1 - items[i].Value / max);
            points.Add(new Point(x, y));
        }

        var poly = new Polyline
        {
            Stroke = lineBrush,
            StrokeThickness = 2,
            Points = points
        };
        canvas.Children.Add(poly);

        for (int i = 0; i < items.Count; i++)
        {
            var x = points[i].X;
            var y = points[i].Y;

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = lineBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(dot, x - dot.Width / 2);
            Canvas.SetTop(dot, y - dot.Height / 2);
            canvas.Children.Add(dot);

            var valueText = new TextBlock
            {
                Text = items[i].Value.ToString("F2", CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(valueText, x + 4);
            Canvas.SetTop(valueText, y - 16);
            canvas.Children.Add(valueText);

            var labelText = new TextBlock
            {
                Text = items[i].Label,
                FontSize = 9,
                Width = 70,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black
            };
            labelText.RenderTransform = new RotateTransform(-45);
            Canvas.SetLeft(labelText, x - 16);
            Canvas.SetTop(labelText, height - 10);
            canvas.Children.Add(labelText);
        }
    }

    private static void DrawPieChart(Canvas canvas, ObservableCollection<BarItem> items)
    {
        canvas.Children.Clear();

        if (items.Count == 0)
            return;

        var width = canvas.ActualWidth;
        if (double.IsNaN(width) || width <= 0)
            width = 720;

        var height = canvas.Height;
        if (double.IsNaN(height) || height <= 0)
            height = 190;

        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(width, height) / 2 - 18;
        if (radius < 10)
            radius = 10;

        var total = 0d;
        foreach (var it in items)
            total += Math.Max(0, it.Value);

        if (total <= 0)
        {
            canvas.Children.Add(new TextBlock
            {
                Text = "Немає даних",
                FontSize = 14,
                Foreground = Brushes.Gray
            });
            return;
        }

        var colors = new[]
        {
            Colors.SteelBlue, Colors.OrangeRed, Colors.MediumSeaGreen, Colors.MediumPurple,
            Colors.Gold, Colors.CadetBlue, Colors.Coral, Colors.RoyalBlue, Colors.DarkKhaki
        };

        var startAngle = -90d;
        var colorIndex = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var value = Math.Max(0, items[i].Value);
            if (value == 0)
                continue;

            var sweep = value / total * 360d;
            var endAngle = startAngle + sweep;

            var startRadians = startAngle * Math.PI / 180d;
            var endRadians = endAngle * Math.PI / 180d;

            var startPoint = new Point(centerX + radius * Math.Cos(startRadians),
                centerY + radius * Math.Sin(startRadians));
            var endPoint = new Point(centerX + radius * Math.Cos(endRadians),
                centerY + radius * Math.Sin(endRadians));

            var largeArc = sweep > 180d;

            var figure = new PathFigure
            {
                StartPoint = new Point(centerX, centerY),
                IsClosed = true
            };
            figure.Segments.Add(new LineSegment(startPoint, true));
            figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, largeArc,
                SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(new Point(centerX, centerY), true));

            var path = new Path
            {
                Fill = new SolidColorBrush(colors[colorIndex % colors.Length]),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Data = new PathGeometry(new[] { figure })
            };
            canvas.Children.Add(path);

            var midAngle = startAngle + sweep / 2d;
            var midRadians = midAngle * Math.PI / 180d;
            var labelRadius = radius * 0.65;
            var labelX = centerX + labelRadius * Math.Cos(midRadians);
            var labelY = centerY + labelRadius * Math.Sin(midRadians);

            var percent = value / total * 100d;
            var tb = new TextBlock
            {
                Text = $"{items[i].Label}\n{items[i].Value:F2}\n{percent:F1}%",
                FontSize = 10,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                Width = 90
            };
            Canvas.SetLeft(tb, labelX - 45);
            Canvas.SetTop(tb, labelY - 10);
            canvas.Children.Add(tb);

            startAngle = endAngle;
            colorIndex++;
        }

        var totalTb = new TextBlock
        {
            Text = $"Σ: {total:F2}",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(totalTb, centerX - 40);
        Canvas.SetTop(totalTb, centerY - 10);
        canvas.Children.Add(totalTb);
    }

    public class BarItem
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
