using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AirLiticApp;

/// <summary>Експорт звітів у .xlsx: таблиці, табличні дані графіків, зображення графіків.</summary>
public static class ReportExcelExporter
{
    private const int MaxSheetNameLen = 31;

    public static void ExportToFile(
        string filePath,
        ReportKind kind,
        DateTime periodFrom,
        DateTime periodTo,
        DataTable mainTable,
        DataTable lostTable,
        IReadOnlyList<WeaponsReportWindow.StackBarItem> mainChartRows,
        IReadOnlyList<WeaponsReportWindow.LostStackSegment> lostChartSegments,
        Stream? mainChartImagePng,
        Stream? lostChartImagePng)
    {
        using var workbook = new XLWorkbook();

        AddParametersSheet(workbook, kind, periodFrom, periodTo);
        AddDataTableSheet(workbook, "Основна таблиця", mainTable);
        AddDataTableSheet(workbook, "Невдалі вильоти", lostTable);
        AddMainChartDataSheet(workbook, mainChartRows);
        AddLostChartDataSheet(workbook, lostChartSegments);

        if (mainChartImagePng != null && mainChartImagePng.Length > 0)
            AddImageSheet(workbook, "Графік вильотів PNG", mainChartImagePng);

        if (lostChartImagePng != null && lostChartImagePng.Length > 0)
            AddImageSheet(workbook, "Графік втрат PNG", lostChartImagePng);

        workbook.SaveAs(filePath);
    }

    private static void AddParametersSheet(XLWorkbook wb, ReportKind kind, DateTime from, DateTime to)
    {
        var ws = wb.Worksheets.Add(SafeSheetName("Параметри"));
        ws.Cell(1, 1).Value = "Тип звіту";
        ws.Cell(1, 2).Value = kind == ReportKind.Weapons ? "Звіт по засобам" : "Звіт по пілотах";
        ws.Cell(2, 1).Value = "Період з";
        ws.Cell(2, 2).Value = from;
        ws.Cell(2, 2).Style.DateFormat.Format = "dd.MM.yyyy";
        ws.Cell(3, 1).Value = "Період по";
        ws.Cell(3, 2).Value = to;
        ws.Cell(3, 2).Style.DateFormat.Format = "dd.MM.yyyy";
        ws.Cell(4, 1).Value = "Сформовано";
        ws.Cell(4, 2).Value = DateTime.Now;
        ws.Cell(4, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
        ws.Columns().AdjustToContents();
    }

    private static void AddDataTableSheet(XLWorkbook wb, string title, DataTable table)
    {
        var name = SafeSheetName(title);
        if (wb.Worksheets.Contains(name))
            name = SafeSheetName(title + "_2");

        var ws = wb.Worksheets.Add(name);

        if (table.Columns.Count == 0)
        {
            ws.Cell(1, 1).Value = "(немає колонок)";
            return;
        }

        if (table.Rows.Count == 0)
        {
            for (var c = 0; c < table.Columns.Count; c++)
                ws.Cell(1, c + 1).Value = table.Columns[c].ColumnName;
            ws.Cell(2, 1).Value = "(немає рядків)";
            ws.Row(1).Style.Font.Bold = true;
            return;
        }

        var inserted = ws.Cell(1, 1).InsertTable(table);
        inserted.ShowAutoFilter = true;
        inserted.Theme = XLTableTheme.TableStyleMedium2;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void AddMainChartDataSheet(XLWorkbook wb, IReadOnlyList<WeaponsReportWindow.StackBarItem> rows)
    {
        var ws = wb.Worksheets.Add(SafeSheetName("Дані графіка вильотів"));
        var headers = new[]
        {
            "Назва (засіб/пілот)", "Всього вильотів", "Удачні (уражено)", "Невдалі", "Інші", "KPI %"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var item in rows)
        {
            ws.Cell(r, 1).Value = item.Label;
            ws.Cell(r, 2).Value = item.TotalCount;
            ws.Cell(r, 3).Value = item.SuccessCount;
            ws.Cell(r, 4).Value = item.FailedCount;
            ws.Cell(r, 5).Value = item.OtherCount;
            var kpi = ParseKpiPercent(item.KpiDisplay);
            if (kpi.HasValue)
                ws.Cell(r, 6).Value = kpi.Value / 100.0;
            else
                ws.Cell(r, 6).Clear();
            if (kpi.HasValue)
                ws.Cell(r, 6).Style.NumberFormat.Format = "0.0%";
            r++;
        }

        if (rows.Count > 0)
            ws.Range(1, 1, r - 1, headers.Length).SetAutoFilter();

        ws.Columns().AdjustToContents();
    }

    private static double? ParseKpiPercent(string display)
    {
        if (string.IsNullOrWhiteSpace(display))
            return null;
        var s = display.Trim().TrimEnd('%').Trim();
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
            return v;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
            return v;
        return null;
    }

    private static void AddLostChartDataSheet(XLWorkbook wb, IReadOnlyList<WeaponsReportWindow.LostStackSegment> segments)
    {
        var ws = wb.Worksheets.Add(SafeSheetName("Дані графіка втрат"));
        ws.Cell(1, 1).Value = "Причина";
        ws.Cell(1, 2).Value = "Кількість (оцінка)";
        ws.Cell(1, 3).Value = "Частка %";
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var seg in segments)
        {
            ws.Cell(r, 1).Value = seg.Label;
            ws.Cell(r, 2).Value = seg.Count;
            ws.Cell(r, 3).Value = seg.HeightPercent / 100.0;
            ws.Cell(r, 3).Style.NumberFormat.Format = "0.00%";
            r++;
        }

        if (segments.Count > 0)
            ws.Range(1, 1, r - 1, 3).SetAutoFilter();

        ws.Columns().AdjustToContents();
    }

    private static void AddImageSheet(XLWorkbook wb, string sheetTitle, Stream pngStream)
    {
        pngStream.Position = 0;
        var ws = wb.Worksheets.Add(SafeSheetName(sheetTitle));
        var picture = ws.AddPicture(pngStream);
        picture.MoveTo(ws.Cell(1, 1));
        var scale = ComputeImageScale(picture.OriginalWidth, picture.OriginalHeight, maxWidthPx: 900, maxHeightPx: 700);
        if (scale < 1)
            picture.Scale(scale);
    }

    private static double ComputeImageScale(int width, int height, int maxWidthPx, int maxHeightPx)
    {
        if (width <= 0 || height <= 0)
            return 1;
        var sx = (double)maxWidthPx / width;
        var sy = (double)maxHeightPx / height;
        var s = Math.Min(1, Math.Min(sx, sy));
        return s <= 0 ? 1 : s;
    }

    private static string SafeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var s = string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch));
        if (s.Length > MaxSheetNameLen)
            s = s[..MaxSheetNameLen];
        if (string.IsNullOrWhiteSpace(s))
            s = "Лист";
        return s;
    }
}
