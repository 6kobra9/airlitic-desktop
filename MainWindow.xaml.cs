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

    private void ReportsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var reportWindow = new WeaponsReportWindow { Owner = this };
        reportWindow.ShowDialog();
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

