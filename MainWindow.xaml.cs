using System;
using System.Collections.ObjectModel;
using System.Data;
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
            var weaponInfoByResultId =
                new System.Collections.Generic.Dictionary<int, (int? WeaponPartId, int? WeaponId, string? WeaponName, string? WeaponTypeName, string? VideoTypeName, string? Serial, string? FrequencyMhz)>();
            var flyingResults = db.FlyingResults.ToDictionary(f => f.Id, f => f.Name);
            var reasons = db.Reasons.ToDictionary(r => r.Id, r => r.Name);
            var subreasons = db.SubreasonLostDrones.ToDictionary(s => s.Id, s => s.Name);
            var subreasonTeches = db.SubreasonTeches.ToDictionary(s => s.Id, s => s.Name);

            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                using var cmd = conn.CreateCommand();
                // Тянем данные по оружию одним join-запросом от results.
                cmd.CommandText = @"
select
    r.id as result_id,
    r.weapon_part_id,
    w.id as weapon_id,
    coalesce(
        nullif(ltrim(rtrim(w.name)), ''),
        nullif(ltrim(rtrim(w.code)), '')
    ) as weapon_name,
    wt.name as weapon_type_name,
    vt.name as video_type_name,
    wp.serial_number,
    wp.frequency_mhz
from results r
left join weapon_parts wp on wp.id = r.weapon_part_id
left join weapon w on w.id = wp.weapon_id
left join weapon_type wt on wt.id = w.type_id
left join video_type vt on vt.id = wp.video_type_id;";

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var resultId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                    if (resultId <= 0) continue;
                    int? weaponPartId = rd.IsDBNull(1) ? null : rd.GetInt32(1);
                    int? weaponId = rd.IsDBNull(2) ? null : rd.GetInt32(2);
                    var weaponName = rd.IsDBNull(3) ? null : rd.GetString(3);
                    var weaponTypeName = rd.IsDBNull(4) ? null : rd.GetString(4);
                    var videoTypeName = rd.IsDBNull(5) ? null : rd.GetString(5);
                    var serial = rd.IsDBNull(6) ? null : rd.GetString(6);
                    var frequencyMhz = rd.IsDBNull(7) ? null : rd.GetDecimal(7).ToString();
                    weaponInfoByResultId[resultId] = (weaponPartId, weaponId, weaponName, weaponTypeName, videoTypeName, serial, frequencyMhz);
                }
            }
            catch
            {
                // Если справочники/колонки еще не созданы — просто оставим пустые значения.
            }

            foreach (var record in db.Records.OrderBy(r => r.Date))
            {
                if (record.PilotId.HasValue && pilots.TryGetValue(record.PilotId.Value, out var pName))
                    record.PilotName = pName;
                else
                    record.PilotName = null;

                if (weaponInfoByResultId.TryGetValue(record.Id, out var weaponInfo))
                {
                    record.WeaponPartId = weaponInfo.WeaponPartId;
                    record.WeaponId = weaponInfo.WeaponId;
                    record.WeaponName = weaponInfo.WeaponName;
                    record.WeaponSerialNumber = weaponInfo.Serial;
                    record.WeaponTypeName = weaponInfo.WeaponTypeName;
                    record.WeaponVideoTypeName = weaponInfo.VideoTypeName;
                    record.WeaponFrequencyMhz = weaponInfo.FrequencyMhz;
                }
                else
                {
                    record.WeaponId = null;
                    record.WeaponPartId = null;
                    record.WeaponName = null;
                    record.WeaponSerialNumber = null;
                    record.WeaponTypeName = null;
                    record.WeaponVideoTypeName = null;
                    record.WeaponFrequencyMhz = null;
                }

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
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження даних: {ex.Message}", "Помилка", MessageBoxButton.OK,
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

    private void ReportsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var reportWindow = new WeaponsReportWindow { Owner = this };
        reportWindow.ShowDialog();
    }

    private void ServiceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Довідка\n\nТел.: 0507506455\nkobra aka marek",
            "Сервіс",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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

