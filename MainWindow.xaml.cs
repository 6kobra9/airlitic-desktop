using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace AirLiticApp;

public partial class MainWindow : Window
{
    private readonly Models.User _currentUser;
    private readonly ObservableCollection<Models.Record> _records = new();

    public MainWindow(Models.User currentUser)
    {
        _currentUser = currentUser;
        InitializeComponent();
        Title += $" (Користувач: {_currentUser.Login}, Роль: {_currentUser.Role})";
        RecordsGrid.ItemsSource = _records;
        ApplyRolePermissions();
        LoadRecords();
    }

    private void ApplyRolePermissions()
    {
        var canEdit = _currentUser.Role is "Admin" or "Editor";
        AddButton.IsEnabled = canEdit;
        EditButton.IsEnabled = canEdit;
        DeleteButton.IsEnabled = _currentUser.Role == "Admin";
        AdminMenuItem.Visibility = _currentUser.Role == "Admin" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadRecords()
    {
        _records.Clear();

        try
        {
            using var db = new Data.AppDbContext();
            var pilots = db.Pilots.ToDictionary(p => p.Id, p => p.Name);
            var weapons = db.Weapons.ToDictionary(w => w.Id, w => w.Name);
            var flyingResults = db.FlyingResults.ToDictionary(f => f.Id, f => f.Name);
            var reasons = db.Reasons.ToDictionary(r => r.Id, r => r.Name);
            var subreasons = db.SubreasonLostDrones.ToDictionary(s => s.Id, s => s.Name);
            var subreasonTeches = db.SubreasonTeches.ToDictionary(s => s.Id, s => s.Name);

            foreach (var record in db.Records.OrderBy(r => r.Date))
            {
                if (record.PilotId.HasValue && pilots.TryGetValue(record.PilotId.Value, out var pName))
                    record.PilotName = pName;
                else
                    record.PilotName = null;

                if (record.WeaponId.HasValue && weapons.TryGetValue(record.WeaponId.Value, out var wName))
                    record.WeaponName = wName;
                else
                    record.WeaponName = null;

                if (record.FlyingResultId.HasValue && flyingResults.TryGetValue(record.FlyingResultId.Value, out var frName))
                    record.FlyingResultName = frName;
                else
                    record.FlyingResultName = null;

                if (record.ReasonId.HasValue && reasons.TryGetValue(record.ReasonId.Value, out var rName))
                    record.ReasonName = rName;
                else
                    record.ReasonName = null;

                if (record.SubreasonLostDroneId.HasValue &&
                    subreasons.TryGetValue(record.SubreasonLostDroneId.Value, out var srName))
                    record.SubreasonLostDroneName = srName;
                else
                    record.SubreasonLostDroneName = null;

                if (record.SubreasonTechId.HasValue &&
                    subreasonTeches.TryGetValue(record.SubreasonTechId.Value, out var stName))
                    record.SubreasonTechName = stName;
                else
                    record.SubreasonTechName = null;

                _records.Add(record);
            }
        }
        catch
        {
            MessageBox.Show(Data.DbHealth.GetUnavailableMessage(), "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RecordEditWindow(null, _currentUser);
        if (dlg.ShowDialog() == true)
        {
            LoadRecords();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordsGrid.SelectedItem is not Models.Record record)
            return;

        var dlg = new RecordEditWindow(record, _currentUser);
        if (dlg.ShowDialog() == true)
        {
            LoadRecords();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordsGrid.SelectedItem is not Models.Record record)
            return;

        if (MessageBox.Show("Видалити запис?", "Підтвердження", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        using var db = new Data.AppDbContext();
        var entity = db.Records.FirstOrDefault(r => r.Id == record.Id);
        if (entity != null)
        {
            db.Records.Remove(entity);
            db.SaveChanges();
        }

        LoadRecords();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private const string ReportDateFromPlaceholder = "{{RPT_DF}}";
    private const string ReportDateToPlaceholder = "{{RPT_DT}}";

    private static string FormatReportSqlDates(string sqlTemplate, DateTime from, DateTime to)
    {
        var df = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dt = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return sqlTemplate
            .Replace(ReportDateFromPlaceholder, df, StringComparison.Ordinal)
            .Replace(ReportDateToPlaceholder, dt, StringComparison.Ordinal);
    }

    private bool TryPickReportPeriod(out DateTime from, out DateTime to)
    {
        var dlg = new ReportPeriodWindow { Owner = this };
        if (dlg.ShowDialog() != true)
        {
            from = to = default;
            return false;
        }

        from = dlg.PeriodFrom;
        to = dlg.PeriodTo;
        return true;
    }

    private void WeaponsReportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPickReportPeriod(out var periodFrom, out var periodTo))
            return;

        const string mainSqlTemplate = @"
declare @colls nvarchar(max);

select @colls = string_agg(quotename(flying_result.name), ',')
from flying_result;

declare @sql nvarchar(max) = N'
select
    weaponName N''Засіб'',
    TotalHits N''Кіл-ть вильотів'',
    ' + @colls + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([Уражено], 0) * 100.0 / TotalHits, 2)
    end N''KPI''
from
(
    select
        p.name as weaponName,
        r.id as ResultId,
        rs.name as ReasonName
    from results r
    left join weapon p on p.id = r.weapon_id
    left join flying_result rs on rs.id = r.flying_result_id
    where r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select count(*) as TotalHits
    from results rf
    where rf.weapon_id = (
        select top(1) id
        from weapon
        where name = p.weaponName
    )
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

        const string lostSqlTemplate = @"
declare @colls nvarchar(max);

select @colls = string_agg(quotename(name), ',')
from subreason_lost_drone;

declare @sql nvarchar(max) = N'
select
    weaponName N''Засіб'',
    TotalHits N''Кіл-ть невдалих вильотів'',
    ' + @colls + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([вороже збиття], 0) * 100 / TotalHits,2)
    end N''KPI вороже збиття''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([реб противника], 0) * 100 / TotalHits,2)
    end N''KPI реб противника''   ,
    case
        when TotalHits = 0 then 0
        else round(isnull([технічні помилки], 0) * 100 / TotalHits,2)
    end N''KPI технічні помилки''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([погодні умови], 0) * 100 / TotalHits,2)
    end N''KPI погодні умови'',
    case
        when TotalHits = 0 then 0
        else round(isnull([реб свій], 0) * 100 / TotalHits,2)
    end N''KPI реб свій'',
    case
        when TotalHits = 0 then 0
        else round(isnull([помилка пілота], 0) * 100 / TotalHits,2)
    end N''KPI помилка пілота''
from
(
    select
        wp.name as weaponName,
        r.id   as ResultId,
        sld.name as ReasonName
from results r
left join weapon wp on wp.id =weapon_id
left join subreason_lost_drone sld on sld.id=r.subreason_lost_drone_id
where flying_result_id=2 and r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId) 
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    where rf.weapon_id = (
        select top(1) id
        from weapon
        where name = p.weaponName
    ) and flying_result_id=2 
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

        var mainSql = FormatReportSqlDates(mainSqlTemplate, periodFrom, periodTo);
        var lostSql = FormatReportSqlDates(lostSqlTemplate, periodFrom, periodTo);

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

            // второй запрос – по невдалих вильотах
            cmd.CommandText = lostSql;
            using var reader2 = cmd.ExecuteReader();
            var lostTable = new DataTable();
            lostTable.Load(reader2);

            var reportWindow = new WeaponsReportWindow(mainTable, lostTable) { Owner = this };
            reportWindow.Title =
                $"Звіт по засобам ({periodFrom:dd.MM.yyyy} — {periodTo:dd.MM.yyyy})";
            reportWindow.ShowDialog();
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
    }

    private void PilotsReportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPickReportPeriod(out var periodFrom, out var periodTo))
            return;

        const string sqlTemplate = @"
declare @colls nvarchar(max);

select @colls = string_agg(quotename(flying_result.name), ',')
from flying_result;

declare @sql nvarchar(max) = N'
select
    PilotName N''Пілот'' ,
    TotalHits N''Кіл-ть вильотів'',
    ' + @colls + ',
    round(case
        when TotalHits = 0 then 0
        else (isnull([Уражено], 0) * 100.00/ TotalHits)
    end,4) N''KPI''
from
(
    select
        p.name as PilotName,
        r.id   as ResultId,
        rs.name as ReasonName
    from results r
    left join pilot         p  on p.id  = r.pilot_id
    left join flying_result rs on rs.id = r.flying_result_id
    where r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId) 
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    where rf.pilot_id = (
        select top(1) id
        from pilot
        where name = p.PilotName
    ) 
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

        const string pilotsLostSqlTemplate = @"
declare @colls nvarchar(max);

select @colls = string_agg(quotename(name), ',')
from subreason_lost_drone;

declare @sql nvarchar(max) = N'
select
    PilotName N''Пілот'',
    TotalHits N''Кіл-ть невдалих вильотів'',
    ' + @colls + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([вороже збиття], 0) * 100 / TotalHits,2)
    end N''KPI вороже збиття''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([реб противника], 0) * 100 / TotalHits,2)
    end N''KPI реб противника''   ,
    case
        when TotalHits = 0 then 0
        else round(isnull([технічні помилки], 0) * 100 / TotalHits,2)
    end N''KPI технічні помилки''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([погодні умови], 0) * 100 / TotalHits,2)
    end N''KPI погодні умови'',
    case
        when TotalHits = 0 then 0
        else round(isnull([реб свій], 0) * 100 / TotalHits,2)
    end N''KPI реб свій'',
    case
        when TotalHits = 0 then 0
        else round(isnull([помилка пілота], 0) * 100 / TotalHits,2)
    end N''KPI помилка пілота''
from
(
    select
        wp.name as PilotName,
        r.id   as ResultId,
        sld.name as ReasonName
    from results r
    left join pilot wp on wp.id = r.pilot_id
    left join subreason_lost_drone sld on sld.id = r.subreason_lost_drone_id
    where r.flying_result_id = 2 and r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    where rf.pilot_id = (
        select top(1) id
        from pilot
        where name = p.PilotName
    ) and rf.flying_result_id = 2
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

        var sql = FormatReportSqlDates(sqlTemplate, periodFrom, periodTo);
        var pilotsLostSql = FormatReportSqlDates(pilotsLostSqlTemplate, periodFrom, periodTo);

        try
        {
            using var db = new Data.AppDbContext();
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var dt = new DataTable();
            dt.Load(reader);

            cmd.CommandText = pilotsLostSql;
            using var readerLost = cmd.ExecuteReader();
            var lostDt = new DataTable();
            lostDt.Load(readerLost);

            var reportWindow = new WeaponsReportWindow(dt, lostDt) { Owner = this };
            reportWindow.Title =
                $"Звіт по пілотах ({periodFrom:dd.MM.yyyy} — {periodTo:dd.MM.yyyy})";
            reportWindow.ShowDialog();
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
    }

    private void SimpleReportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        using var db = new Data.AppDbContext();
        MessageBox.Show($"Всього записів: {db.Records.Count()}",
            "Звіт", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PilotsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SimpleDictionaryWindow(SimpleDictionaryKind.Pilots) { Owner = this };
        dlg.ShowDialog();
    }

    private void WeaponsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SimpleDictionaryWindow(SimpleDictionaryKind.Weapons) { Owner = this };
        dlg.ShowDialog();
    }

    private void LostDroneReasonsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SimpleDictionaryWindow(SimpleDictionaryKind.SubreasonLostDrone) { Owner = this };
        dlg.ShowDialog();
    }

    private void TechProblemsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SimpleDictionaryWindow(SimpleDictionaryKind.SubreasonTech) { Owner = this };
        dlg.ShowDialog();
    }

    private void AddUserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddUserWindow { Owner = this };
        dlg.ShowDialog();
    }
}

