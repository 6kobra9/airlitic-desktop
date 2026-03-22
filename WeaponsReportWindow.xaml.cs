using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace AirLiticApp;

public partial class WeaponsReportWindow : Window
{
    public ObservableCollection<LostStackSegment> LostStackSegments { get; } = new();
    public ObservableCollection<StackBarItem> MainStackItems { get; } = new();

    private DataTable? _exportMainTable;
    private DataTable? _exportLostTable;
    private DateTime _exportFrom;
    private DateTime _exportTo;
    private ReportKind _exportKind;
    private bool _hasExportData;

    public WeaponsReportWindow()
    {
        InitializeComponent();
        DataContext = this;

        var today = DateTime.Today;
        FromDatePicker.SelectedDate = today;
        ToDatePicker.SelectedDate = today;

        ClearResultsUi();
        UpdateHeadersForKind(GetSelectedKind());
    }

    private ReportKind GetSelectedKind()
    {
        if (ReportTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is ReportKind k)
            return k;
        return ReportKind.Weapons;
    }

    private void ReportTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        // Після зміни типу — знову потрібне «Сформувати»; графіки не показуємо
        ClearResultsUi();
        UpdateHeadersForKind(GetSelectedKind());
    }

    private void UpdateHeadersForKind(ReportKind kind)
    {
        MainTableGroup.Header = kind == ReportKind.Weapons
            ? "Загальний звіт по засобам"
            : "Загальний звіт по пілотах";
        LostTableGroup.Header = "Звіт по невдалих вильотах (причини втрати)";
    }

    private void ClearResultsUi()
    {
        ReportGrid.ItemsSource = null;
        ReportGrid.Columns.Clear();
        LostReportGrid.ItemsSource = null;
        LostReportGrid.Columns.Clear();
        MainStackItems.Clear();
        LostStackSegments.Clear();
        MainTableFooterSumText.Text = string.Empty;
        MainTableFooterPanel.Visibility = Visibility.Collapsed;
        LostTableFooterSumText.Text = string.Empty;
        LostTableFooterPanel.Visibility = Visibility.Collapsed;

        MainHitsChartGroup.Visibility = Visibility.Collapsed;
        LostReasonsChartGroup.Visibility = Visibility.Collapsed;
        MainChartAxisText.Text = string.Empty;
        LostChartAxisText.Text = string.Empty;

        _exportMainTable = null;
        _exportLostTable = null;
        _hasExportData = false;
        ExportExcelButton.IsEnabled = false;

        LostTableGroup.Visibility = Visibility.Visible;
        if (RootGrid.RowDefinitions.Count > 2)
            RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var kind = GetSelectedKind();
        var from = FromDatePicker.SelectedDate?.Date ?? DateTime.Today;
        var to = ToDatePicker.SelectedDate?.Date ?? DateTime.Today;
        if (from > to)
            (from, to) = (to, from);

        var mainSql = ReportSql.ApplyDates(
            kind == ReportKind.Weapons ? ReportSql.WeaponsMainTemplate : ReportSql.PilotsMainTemplate,
            from, to);
        var lostSql = ReportSql.ApplyDates(
            kind == ReportKind.Weapons ? ReportSql.WeaponsLostTemplate : ReportSql.PilotsLostTemplate,
            from, to);

        GenerateButton.IsEnabled = false;
        try
        {
            using var db = new Data.AppDbContext();
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = mainSql;
            using var reader = cmd.ExecuteReader();
            var mainTable = new DataTable();
            mainTable.Load(reader);

            cmd.CommandText = lostSql;
            using var reader2 = cmd.ExecuteReader();
            var lostTable = new DataTable();
            lostTable.Load(reader2);

            ApplyReportData(mainTable, lostTable, kind, from, to);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Data.DbHealth.IsDatabaseAvailable()
                    ? ex.Message
                    : Data.DbHealth.GetUnavailableMessage(),
                "Помилка звіту",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void ApplyReportData(DataTable mainReport, DataTable lostReport, ReportKind kind, DateTime periodFrom,
        DateTime periodTo)
    {
        Title = kind == ReportKind.Weapons
            ? $"Звіт по засобам ({periodFrom:dd.MM.yyyy} — {periodTo:dd.MM.yyyy})"
            : $"Звіт по пілотах ({periodFrom:dd.MM.yyyy} — {periodTo:dd.MM.yyyy})";

        UpdateHeadersForKind(kind);

        BindDataTableToGrid(ReportGrid, mainReport);
        ReportGrid.ItemsSource = mainReport.DefaultView;
        BuildMainTableFooter(mainReport);
        BuildMainStackChart(mainReport, lostReport);

        BindDataTableToGrid(LostReportGrid, lostReport);
        LostReportGrid.ItemsSource = lostReport.DefaultView;
        BuildLostTableFooter(lostReport);
        BuildLostStackedChart(lostReport);

        LostTableGroup.Visibility = Visibility.Visible;
        if (RootGrid.RowDefinitions.Count > 2)
            RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

        MainHitsChartGroup.Visibility = Visibility.Visible;
        LostReasonsChartGroup.Visibility = Visibility.Visible;

        _exportMainTable = mainReport;
        _exportLostTable = lostReport;
        _exportFrom = periodFrom;
        _exportTo = periodTo;
        _exportKind = kind;
        _hasExportData = true;
        ExportExcelButton.IsEnabled = true;
    }

    private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasExportData || _exportMainTable == null || _exportLostTable == null)
        {
            MessageBox.Show("Спочатку натисніть «Сформувати», щоб отримати дані для експорту.",
                "Експорт у Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Усі файли (*.*)|*.*",
            FileName = $"Звіт_{_exportKind}_{_exportFrom:yyyyMMdd}-{_exportTo:yyyyMMdd}.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true)
            return;

        MemoryStream? mainPng = null;
        MemoryStream? lostPng = null;
        try
        {
            TryRenderElementToPng(MainHitsChartGroup, out mainPng);
            TryRenderElementToPng(LostReasonsChartGroup, out lostPng);

            ReportExcelExporter.ExportToFile(
                dlg.FileName,
                _exportKind,
                _exportFrom,
                _exportTo,
                _exportMainTable,
                _exportLostTable,
                MainStackItems.ToList(),
                LostStackSegments.ToList(),
                mainPng,
                lostPng);

            MessageBox.Show($"Файл збережено:\n{dlg.FileName}", "Експорт у Excel",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не вдалося зберегти Excel:\n{ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            mainPng?.Dispose();
            lostPng?.Dispose();
        }
    }

    /// <summary>Рендерить WPF-елемент у PNG для вставки в Excel.</summary>
    private static bool TryRenderElementToPng(FrameworkElement element, out MemoryStream? stream)
    {
        stream = null;
        try
        {
            if (element.Visibility != Visibility.Visible)
                return false;

            element.UpdateLayout();
            var w = element.ActualWidth;
            var h = element.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                element.Arrange(new Rect(element.DesiredSize));
                w = element.DesiredSize.Width;
                h = element.DesiredSize.Height;
            }

            if (w <= 0 || h <= 0)
                return false;

            var pw = Math.Max(1, (int)Math.Ceiling(w));
            var ph = Math.Max(1, (int)Math.Ceiling(h));
            const double dpi = 96;
            var bmp = new RenderTargetBitmap(pw, ph, dpi, dpi, PixelFormats.Pbgra32);
            bmp.Render(element);
            stream = new MemoryStream();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            enc.Save(stream);
            stream.Position = 0;
            return true;
        }
        catch
        {
            stream?.Dispose();
            stream = null;
            return false;
        }
    }

    private void BuildMainTableFooter(DataTable table)
    {
        var col = FindTotalFlightsColumn(table, lostFlights: false);
        if (col == null)
        {
            MainTableFooterSumText.Text = string.Empty;
            MainTableFooterPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var sum = SumColumn(table, col);
        MainTableFooterSumText.Text =
            $"Сума «{col.ColumnName}»: {FormatFooterSum(sum)}";
        MainTableFooterPanel.Visibility = Visibility.Visible;
    }

    private void BuildLostTableFooter(DataTable table)
    {
        var col = FindTotalFlightsColumn(table, lostFlights: true);
        if (col == null)
        {
            LostTableFooterSumText.Text = string.Empty;
            LostTableFooterPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var sum = SumColumn(table, col);
        LostTableFooterSumText.Text =
            $"Сума «{col.ColumnName}»: {FormatFooterSum(sum)}";
        LostTableFooterPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Колонка «Кіл-ть вильотів» (основна) або «Кіл-ть невдалих вильотів» (lost).</summary>
    private static DataColumn? FindTotalFlightsColumn(DataTable table, bool lostFlights)
    {
        if (lostFlights)
        {
            if (table.Columns.Contains("Кіл-ть невдалих вильотів"))
                return table.Columns["Кіл-ть невдалих вильотів"];
            if (table.Columns.Contains("TotalHits"))
                return table.Columns["TotalHits"];
            foreach (DataColumn col in table.Columns)
            {
                var n = NormalizeFooterColumnName(col.ColumnName);
                if (n.Contains("невдал", StringComparison.OrdinalIgnoreCase) &&
                    n.Contains("вильот", StringComparison.OrdinalIgnoreCase))
                    return col;
            }
        }
        else
        {
            if (table.Columns.Contains("Кіл-ть вильотів"))
                return table.Columns["Кіл-ть вильотів"];
            if (table.Columns.Contains("TotalHits"))
                return table.Columns["TotalHits"];
            foreach (DataColumn col in table.Columns)
            {
                var n = NormalizeFooterColumnName(col.ColumnName);
                if (n.Contains("кіл", StringComparison.OrdinalIgnoreCase) &&
                    n.Contains("вильот", StringComparison.OrdinalIgnoreCase) &&
                    !n.Contains("невдал", StringComparison.OrdinalIgnoreCase))
                    return col;
            }
        }

        return null;
    }

    private static string NormalizeFooterColumnName(string name) =>
        name.Replace('\u00A0', ' ').Replace('\u202F', ' ').Trim();

    private static double SumColumn(DataTable table, DataColumn col)
    {
        double sum = 0;
        foreach (DataRow row in table.Rows)
        {
            if (TryToDouble(row[col], out var v) && !double.IsNaN(v) && !double.IsInfinity(v))
                sum += v;
        }

        return sum;
    }

    private static string FormatFooterSum(double sum)
    {
        if (Math.Abs(sum - Math.Round(sum)) < 0.0005)
            return Math.Round(sum).ToString("N0", CultureInfo.CurrentCulture);
        return sum.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static void BindDataTableToGrid(DataGrid grid, DataTable table)
    {
        grid.Columns.Clear();
        foreach (DataColumn dc in table.Columns)
        {
            var escaped = dc.ColumnName.Replace("]", "]]", StringComparison.Ordinal);
            var binding = new Binding
            {
                Path = new PropertyPath($"[{escaped}]"),
                Mode = BindingMode.OneWay
            };
            // Усі колонки, у назві яких є «KPI» (основна KPI та KPI … у нижній таблиці)
            if (IsKpiFormattedColumn(dc))
            {
                binding.StringFormat = "N1";
                binding.ConverterCulture = CultureInfo.CurrentCulture;
            }

            var col = new DataGridTextColumn
            {
                Header = dc.ColumnName,
                Binding = binding,
                SortMemberPath = dc.ColumnName,
                MinWidth = 48,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            grid.Columns.Add(col);
        }
    }

    private static bool IsKpiFormattedColumn(DataColumn dc)
    {
        var n = NormalizeReportColumnHeader(dc.ColumnName);
        return n.Contains("KPI", StringComparison.OrdinalIgnoreCase);
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

    private void BuildMainStackChart(DataTable mainReport, DataTable lostReport)
    {
        MainStackItems.Clear();
        MainChartAxisText.Text = string.Empty;

        if (!TryResolveMainTotalAndHitsColumns(mainReport, out var totalCol, out var hitsCol))
            return;

        double maxTotal = 0d;
        foreach (DataRow row in mainReport.Rows)
        {
            var t = TryGetDouble(row[totalCol!]);
            if (t > maxTotal) maxTotal = t;
        }

        if (maxTotal <= 0)
            return;

        var kpiCol = FindKpiColumn(mainReport);
        UpdateMainChartAxisTicks(maxTotal);

        foreach (DataRow row in mainReport.Rows)
        {
            var label = row[0]?.ToString()?.Trim() ?? string.Empty;
            var total = TryGetDouble(row[totalCol!]);
            var successful = TryGetDouble(row[hitsCol!]);
            var failed = LookupLostFlightsCountForLabel(lostReport, label);

            if (successful > total)
                successful = total;
            failed = Math.Min(failed, Math.Max(0, total - successful));
            var other = Math.Max(0, total - successful - failed);

            var kpiDisplay = string.Empty;
            if (kpiCol != null && TryToDouble(row[kpiCol], out var kpiVal))
                kpiDisplay = $"{Math.Round(kpiVal, MidpointRounding.AwayFromZero)}%";

            MainStackItems.Add(new StackBarItem
            {
                Label = label,
                TotalCount = total,
                SuccessCount = successful,
                FailedCount = failed,
                OtherCount = other,
                SuccessTrackPercent = 100d * successful / maxTotal,
                FailedTrackPercent = 100d * failed / maxTotal,
                OtherTrackPercent = 100d * other / maxTotal,
                KpiDisplay = kpiDisplay
            });
        }
    }

    /// <summary>Колонки «Кіл-ть вильотів» та «Уражено» для основного звіту.</summary>
    private static bool TryResolveMainTotalAndHitsColumns(DataTable table, out string? totalCol, out string? hitsCol)
    {
        totalCol = null;
        hitsCol = null;

        if (table.Columns.Contains("Кіл-ть вильотів"))
            totalCol = "Кіл-ть вильотів";
        else if (table.Columns.Contains("TotalHits"))
            totalCol = "TotalHits";
        else
        {
            foreach (DataColumn col in table.Columns)
            {
                var n = col.ColumnName;
                if (n.Contains("Кіл", StringComparison.OrdinalIgnoreCase) &&
                    n.Contains("виль", StringComparison.OrdinalIgnoreCase) &&
                    !n.Contains("невдал", StringComparison.OrdinalIgnoreCase))
                {
                    totalCol = n;
                    break;
                }
            }
        }

        if (totalCol == null)
            return false;

        foreach (DataColumn col in table.Columns)
        {
            var n = col.ColumnName;
            var norm = NormalizeReportColumnHeader(n);
            if (norm.Equals("Уражено", StringComparison.OrdinalIgnoreCase))
                hitsCol = n;
        }

        if (hitsCol == null)
        {
            foreach (DataColumn col in table.Columns)
            {
                var n = col.ColumnName;
                if (n.Contains("Ураж", StringComparison.OrdinalIgnoreCase) &&
                    !n.Contains("Не", StringComparison.OrdinalIgnoreCase))
                {
                    hitsCol = n;
                    break;
                }
            }
        }

        return hitsCol != null;
    }

    /// <summary>Кількість невдалих вильотів з нижньої таблиці для того ж засобу/пілота.</summary>
    private static double LookupLostFlightsCountForLabel(DataTable lostReport, string label)
    {
        if (lostReport.Rows.Count == 0 || string.IsNullOrWhiteSpace(label))
            return 0d;

        var failedCol = FindTotalFlightsColumn(lostReport, lostFlights: true);
        if (failedCol == null)
            return 0d;

        var key = label.Trim();
        foreach (DataRow r in lostReport.Rows)
        {
            var name = r[0]?.ToString()?.Trim() ?? string.Empty;
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                return TryGetDouble(r[failedCol]);
        }

        return 0d;
    }

    private void UpdateMainChartAxisTicks(double maxTotal)
    {
        var m = Math.Max(1d, maxTotal);
        var a0 = 0d;
        var a1 = Math.Round(m / 4d, MidpointRounding.AwayFromZero);
        var a2 = Math.Round(m / 2d, MidpointRounding.AwayFromZero);
        var a3 = Math.Round(3d * m / 4d, MidpointRounding.AwayFromZero);
        var a4 = Math.Round(m, MidpointRounding.AwayFromZero);
        var fmt = (double v) => v.ToString("N0", CultureInfo.CurrentCulture);
        MainChartAxisText.Text =
            $"{fmt(a0)}          {fmt(a1)}          {fmt(a2)}          {fmt(a3)}          {fmt(a4)}";
    }

    private static DataColumn? FindKpiColumn(DataTable table)
    {
        foreach (DataColumn col in table.Columns)
        {
            var norm = NormalizeReportColumnHeader(col.ColumnName);
            if (norm.Equals("KPI", StringComparison.OrdinalIgnoreCase))
                return col;
        }

        return null;
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

    private void BuildLostStackedChart(DataTable table)
    {
        LostStackSegments.Clear();
        LostChartAxisText.Text = string.Empty;

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

        var total = sumPilot + sumTech + sumVorezh + sumOwnReb + sumEnemyReb + sumWeather;
        if (total <= 0)
            return;

        LostChartAxisText.Text = string.Format(CultureInfo.CurrentCulture,
            "0          {0:N0}          {1:N0}          {2:N0}          {3:N0}",
            Math.Round(total / 4d, MidpointRounding.AwayFromZero),
            Math.Round(total / 2d, MidpointRounding.AwayFromZero),
            Math.Round(3d * total / 4d, MidpointRounding.AwayFromZero),
            Math.Round(total, MidpointRounding.AwayFromZero));

        void AddSegment(string label, double count, Color barColor, bool lightBar)
        {
            var brush = new SolidColorBrush(barColor);
            brush.Freeze();
            Brush fg;
            if (lightBar)
            {
                var dark = new SolidColorBrush(Color.FromRgb(0x59, 0x0C, 0x6D));
                dark.Freeze();
                fg = dark;
            }
            else
                fg = Brushes.White;

            LostStackSegments.Add(new LostStackSegment
            {
                Label = label,
                Count = count,
                HeightPercent = count / total * 100d,
                BarBrush = brush,
                CountForeground = fg
            });
        }

        // Гама фіолетово-пурпурова (легко розрізняються, зліва направо у смузі)
        AddSegment("Помилка пілота", sumPilot, Color.FromRgb(0x3B, 0x07, 0x64), lightBar: false);
        AddSegment("Технічні помилки", sumTech, Color.FromRgb(0x5B, 0x21, 0xB6), lightBar: false);
        AddSegment("Вороже збиття", sumVorezh, Color.FromRgb(0x7C, 0x2D, 0x92), lightBar: false);
        AddSegment("Реб свій", sumOwnReb, Color.FromRgb(0xA2, 0x1C, 0xAF), lightBar: false);
        AddSegment("Реб противника", sumEnemyReb, Color.FromRgb(0xC0, 0x26, 0xD3), lightBar: false);
        AddSegment("Погодні умови", sumWeather, Color.FromRgb(0xF5, 0xD0, 0xFE), lightBar: true);
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
        /// <summary>Успішні (уражено).</summary>
        public double SuccessCount { get; set; }
        /// <summary>Невдалі вильоти (з нижнього звіту).</summary>
        public double FailedCount { get; set; }
        /// <summary>Решта вильотів (не ураження й не з нижнього звіту як невдалі).</summary>
        public double OtherCount { get; set; }
        /// <summary>Частка від максимального «Кіл-ть вильотів» по всіх рядках (0–100) для ширини смуги на осі X.</summary>
        public double SuccessTrackPercent { get; set; }
        public double FailedTrackPercent { get; set; }
        public double OtherTrackPercent { get; set; }
        /// <summary>KPI з таблиці, округлений до цілого у відсотках (напр. «42%») або порожньо.</summary>
        public string KpiDisplay { get; set; } = string.Empty;

        public string HoverSummary =>
            string.Format(
                CultureInfo.CurrentCulture,
                "{0}\nВсього вильотів: {1:N0}\nУдачні (уражено): {2:N0}\nНевдалі: {3:N0}\nІнші: {4:N0}",
                Label,
                TotalCount,
                SuccessCount,
                FailedCount,
                OtherCount);
    }

    public class LostStackSegment
    {
        public string Label { get; set; } = string.Empty;
        public double Count { get; set; }
        public double HeightPercent { get; set; }
        public Brush BarBrush { get; set; } = Brushes.Gray;
        public Brush CountForeground { get; set; } = Brushes.White;
    }
}
