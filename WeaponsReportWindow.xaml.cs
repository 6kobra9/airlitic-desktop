using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AirLiticApp;

public partial class WeaponsReportWindow : Window
{
    public ObservableCollection<LostReasonBarItem> LostReasonItems { get; } = new();
    public ObservableCollection<StackBarItem> MainStackItems { get; } = new();

    public WeaponsReportWindow(DataTable mainReport, DataTable? lostReport = null)
    {
        InitializeComponent();
        DataContext = this;

        BindDataTableToGrid(ReportGrid, mainReport);
        ReportGrid.ItemsSource = mainReport.DefaultView;
        BuildMainStackChart(mainReport);

        if (lostReport != null)
        {
            BindDataTableToGrid(LostReportGrid, lostReport);
            LostReportGrid.ItemsSource = lostReport.DefaultView;
            BuildLostChart(lostReport);
        }
        else
        {
            LostReportGrid.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Явні колонки для DataTable.DefaultView: індексатор DataRowView [ім'я] — інакше колонки з пробілом
    /// («Не уражено», «Кіл-ть вильотів») не показуються при AutoGenerateColumns.
    /// </summary>
    private static void BindDataTableToGrid(DataGrid grid, DataTable table)
    {
        grid.Columns.Clear();
        foreach (DataColumn dc in table.Columns)
        {
            var escaped = dc.ColumnName.Replace("]", "]]", StringComparison.Ordinal);
            var col = new DataGridTextColumn
            {
                Header = dc.ColumnName,
                Binding = new Binding
                {
                    Path = new PropertyPath($"[{escaped}]"),
                    Mode = BindingMode.OneWay
                },
                SortMemberPath = dc.ColumnName,
                MinWidth = 48,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            grid.Columns.Add(col);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string NormalizeReportColumnHeader(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        return name
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('\u2009', ' ')
            .Trim();
    }

    private void BuildMainStackChart(DataTable table)
    {
        MainStackItems.Clear();

        var totalCol =
            table.Columns.Contains("Кіл-ть вильотів")
                ? "Кіл-ть вильотів"
                : table.Columns.Contains("TotalHits")
                    ? "TotalHits"
                    : null;

        if (totalCol == null)
        {
            foreach (DataColumn col in table.Columns)
            {
                var n = col.ColumnName;
                if (n.Contains("Кіл", StringComparison.OrdinalIgnoreCase) &&
                    n.Contains("виль", StringComparison.OrdinalIgnoreCase))
                {
                    totalCol = n;
                    break;
                }
            }
        }

        if (totalCol == null)
            return;

        string? hitsCol = null;
        string? missesCol = null;
        foreach (DataColumn col in table.Columns)
        {
            var n = col.ColumnName;
            var norm = NormalizeReportColumnHeader(n);
            if (norm.Equals("Уражено", StringComparison.OrdinalIgnoreCase))
                hitsCol = n;
            if (norm.Equals("Не уражено", StringComparison.OrdinalIgnoreCase) ||
                norm.Equals("Не уражент", StringComparison.OrdinalIgnoreCase))
                missesCol = n;
        }

        if (hitsCol == null || missesCol == null)
        {
            foreach (DataColumn col in table.Columns)
            {
                var n = col.ColumnName;
                var hasUraz = n.Contains("Ураж", StringComparison.OrdinalIgnoreCase);
                var hasNe = n.Contains("Не", StringComparison.OrdinalIgnoreCase);

                if (missesCol == null && hasUraz && hasNe)
                    missesCol = n;
                else if (hitsCol == null && hasUraz && !hasNe)
                    hitsCol = n;
            }
        }

        if (hitsCol == null || missesCol == null)
            return;

        double maxTotal = 0d;
        foreach (DataRow row in table.Rows)
        {
            var total = TryGetDouble(row[totalCol]);
            if (total > maxTotal) maxTotal = total;
        }

        if (maxTotal <= 0)
            return;

        foreach (DataRow row in table.Rows)
        {
            var label = row[0]?.ToString() ?? string.Empty;
            var total = TryGetDouble(row[totalCol]);
            var hits = TryGetDouble(row[hitsCol]);
            var misses = TryGetDouble(row[missesCol]);

            var totalPercent = total / maxTotal * 100d;
            var hitsPercent = hits / maxTotal * 100d;
            var missesPercent = misses / maxTotal * 100d;

            MainStackItems.Add(new StackBarItem
            {
                Label = label,
                TotalCount = total,
                HitsCount = hits,
                MissesCount = misses,
                TotalPercent = totalPercent,
                HitsPercent = hitsPercent,
                MissesPercent = missesPercent
            });
        }
    }

    private static double TryGetDouble(object? value)
    {
        if (value == null || value == DBNull.Value)
            return 0d;
        return value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
        };
    }

    private void BuildLostChart(DataTable table)
    {
        LostReasonItems.Clear();

        var totalHitsColumn = table.Columns.Contains("TotalHits")
            ? "TotalHits"
            : table.Columns.Contains("Кіл-ть невдалих вильотів")
                ? "Кіл-ть невдалих вильотів"
                : null;

        if (totalHitsColumn == null)
            return;

        double sumPilot = 0, sumTech = 0, sumVorezh = 0, sumOwnReb = 0, sumEnemyReb = 0, sumWeather = 0;

        foreach (DataRow row in table.Rows)
        {
            if (!TryToDouble(row[totalHitsColumn], out var hits) || hits <= 0)
                continue;

            var pilotErrPct = table.Columns.Contains("KPI помилка пілота") && TryToDouble(row["KPI помилка пілота"], out var pe) ? pe : 0d;
            var techErrPct = table.Columns.Contains("KPI технічні помилки") && TryToDouble(row["KPI технічні помилки"], out var te) ? te : 0d;
            var vorezhPct = table.Columns.Contains("KPI вороже збиття") && TryToDouble(row["KPI вороже збиття"], out var ve) ? ve : 0d;
            var ownRebPct = table.Columns.Contains("KPI реб свій") && TryToDouble(row["KPI реб свій"], out var ore) ? ore : 0d;
            var enemyRebPct = table.Columns.Contains("KPI реб противника") && TryToDouble(row["KPI реб противника"], out var ere) ? ere : 0d;
            var weatherPct = table.Columns.Contains("KPI погодні умови") && TryToDouble(row["KPI погодні умови"], out var wte) ? wte : 0d;

            sumPilot += Math.Round(hits * pilotErrPct / 100d, 0);
            sumTech += Math.Round(hits * techErrPct / 100d, 0);
            sumVorezh += Math.Round(hits * vorezhPct / 100d, 0);
            sumOwnReb += Math.Round(hits * ownRebPct / 100d, 0);
            sumEnemyReb += Math.Round(hits * enemyRebPct / 100d, 0);
            sumWeather += Math.Round(hits * weatherPct / 100d, 0);
        }

        var max = new[] { sumPilot, sumTech, sumVorezh, sumOwnReb, sumEnemyReb, sumWeather }.Max();
        if (max <= 0)
            return;

        void AddReason(string label, double count, Color barColor, bool lightBar)
        {
            var brush = new SolidColorBrush(barColor);
            brush.Freeze();
            Brush fg;
            if (lightBar)
            {
                var dark = new SolidColorBrush(Color.FromRgb(0x0B, 0x35, 0x54));
                dark.Freeze();
                fg = dark;
            }
            else
                fg = Brushes.White;

            LostReasonItems.Add(new LostReasonBarItem
            {
                Label = label,
                Count = count,
                HeightPercent = count / max * 100d,
                BarBrush = brush,
                CountForeground = fg
            });
        }

        // Порядок як у легенді: зверху вниз — той самий зліва направо під стовпчиками
        AddReason("Помилка пілота", sumPilot, Color.FromRgb(0x1B, 0x4F, 0x72), lightBar: false);
        AddReason("Технічні помилки", sumTech, Color.FromRgb(0x2E, 0x6F, 0xBF), lightBar: false);
        AddReason("Вороже збиття", sumVorezh, Color.FromRgb(0x3A, 0x7B, 0xD5), lightBar: false);
        AddReason("Реб свій", sumOwnReb, Color.FromRgb(0x4F, 0x91, 0xE6), lightBar: false);
        AddReason("Реб противника", sumEnemyReb, Color.FromRgb(0x66, 0xA6, 0xFF), lightBar: false);
        AddReason("Погодні умови", sumWeather, Color.FromRgb(0xBF, 0xE3, 0xFF), lightBar: true);
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

    public class StackBarItem
    {
        public string Label { get; set; } = string.Empty;

        public double TotalCount { get; set; }
        public double HitsCount { get; set; }
        public double MissesCount { get; set; }

        public double TotalPercent { get; set; }
        public double HitsPercent { get; set; }
        public double MissesPercent { get; set; }
    }

    public class LostReasonBarItem
    {
        public string Label { get; set; } = string.Empty;
        public double Count { get; set; }
        /// <summary>Висота стовпчика відносно максимальної причини (0–100).</summary>
        public double HeightPercent { get; set; }
        public Brush BarBrush { get; set; } = Brushes.Gray;
        public Brush CountForeground { get; set; } = Brushes.White;
    }
}
