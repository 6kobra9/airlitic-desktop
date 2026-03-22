using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace AirLiticApp;

public partial class RecordEditWindow : Window
{
    private readonly Models.Record? _record;
    private readonly Models.User _currentUser;

    public RecordEditWindow(Models.Record? record, Models.User currentUser)
    {
        _record = record;
        _currentUser = currentUser;
        InitializeComponent();

        try
        {
            LoadDictionaries();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження довідників: {ex.Message}",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (_record != null)
        {
            DatePicker.Value = _record.Date;
            if (_record.Time is TimeSpan t)
            {
                HoursUpDown.Value = t.Hours;
                MinutesUpDown.Value = t.Minutes;
            }
            else
            {
                HoursUpDown.Value = null;
                MinutesUpDown.Value = null;
            }

            PilotComboBox.SelectedValue = _record.PilotId;
            WeaponComboBox.SelectedValue = _record.WeaponId;
            FlyingResultComboBox.SelectedValue = _record.FlyingResultId;
            ReasonComboBox.SelectedValue = _record.ReasonId;
            SubreasonLostDroneComboBox.SelectedValue = _record.SubreasonLostDroneId;
            SubreasonTechComboBox.SelectedValue = _record.SubreasonTechId;
            DescriptionTextBox.Text = _record.Description;
            Title = "Зміна запису";

            UpdateReasonEnabled();
            UpdateSubreasonLostDroneVisibility();
            UpdateSubreasonTechVisibility();
        }
        else
        {
            DatePicker.Value = DateTime.Today;
            HoursUpDown.Value = null;
            MinutesUpDown.Value = null;
            Title = "Новий запис";
            ReasonComboBox.IsEnabled = false;
            SubreasonLostDroneComboBox.IsEnabled = false;
            SubreasonLostDroneComboBox.SelectedIndex = -1;
            SubreasonTechComboBox.IsEnabled = false;
            SubreasonTechComboBox.SelectedIndex = -1;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var missing = new List<string>();
        if (DatePicker.Value == null)
            missing.Add("дата");
        if (PilotComboBox.SelectedValue as int? == null)
            missing.Add("пілот");
        if (WeaponComboBox.SelectedValue as int? == null)
            missing.Add("озброєння");
        if (FlyingResultComboBox.SelectedValue as int? == null)
            missing.Add("результат польоту");

        if (missing.Count > 0)
        {
            MessageBox.Show(
                "Заповніть обов'язкові поля: " + string.Join(", ", missing) + ".",
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        TimeSpan? time = null;
        var h = HoursUpDown.Value;
        var m = MinutesUpDown.Value;
        if (h.HasValue || m.HasValue)
        {
            var hh = Math.Clamp(h ?? 0, 0, 23);
            var mm = Math.Clamp(m ?? 0, 0, 59);
            time = new TimeSpan(0, hh, mm, 0);
        }

        int? pilotId = PilotComboBox.SelectedValue as int?;
        int? weaponId = WeaponComboBox.SelectedValue as int?;
        int? flyingResultId = FlyingResultComboBox.SelectedValue as int?;
        int? reasonId = ReasonComboBox.SelectedValue as int?;
        int? subreasonLostDroneId = SubreasonLostDroneComboBox.SelectedValue as int?;
        int? subreasonTechId = SubreasonTechComboBox.SelectedValue as int?;

        using var db = new Data.AppDbContext();

        if (_record == null)
        {
            var newRecord = new Models.Record
            {
                Date = DatePicker.Value!.Value.Date,
                Time = time,
                PilotId = pilotId,
                WeaponId = weaponId,
                FlyingResultId = flyingResultId,
                ReasonId = reasonId,
                SubreasonLostDroneId = subreasonLostDroneId,
                SubreasonTechId = subreasonTechId,
                Description = DescriptionTextBox.Text.Trim(),
                UserId = _currentUser.Id,
                Dlc = DateTime.Now
            };
            db.Records.Add(newRecord);
        }
        else
        {
            var entity = db.Records.Find(_record.Id);
            if (entity != null)
            {
                entity.Date = DatePicker.Value!.Value.Date;
                entity.Time = time;
                entity.PilotId = pilotId;
                entity.WeaponId = weaponId;
                entity.FlyingResultId = flyingResultId;
                entity.ReasonId = reasonId;
                entity.SubreasonLostDroneId = subreasonLostDroneId;
                entity.SubreasonTechId = subreasonTechId;
                entity.Description = DescriptionTextBox.Text.Trim();
                entity.UserId = _currentUser.Id;
                entity.Dlc = DateTime.Now;
            }
        }

        db.SaveChanges();
        DialogResult = true;
        Close();
    }

    private void LoadDictionaries()
    {
        using var db = new Data.AppDbContext();
        PilotComboBox.ItemsSource = db.Pilots
            .OrderBy(p => p.Name)
            .ToList();
        WeaponComboBox.ItemsSource = db.Weapons
            .OrderBy(w => w.Name)
            .ToList();
        FlyingResultComboBox.ItemsSource = db.FlyingResults
            .OrderBy(f => f.Name)
            .ToList();
        ReasonComboBox.ItemsSource = db.Reasons
            .OrderBy(r => r.Name)
            .ToList();
        SubreasonLostDroneComboBox.ItemsSource = db.SubreasonLostDrones
            .OrderBy(s => s.Name)
            .ToList();
        SubreasonTechComboBox.ItemsSource = db.SubreasonTeches
            .OrderBy(s => s.Name)
            .ToList();
    }

    private void FlyingResultComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateReasonEnabled();
        UpdateSubreasonLostDroneVisibility();
    }

    private void ReasonComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSubreasonLostDroneVisibility();
        UpdateSubreasonTechVisibility();
    }

    private void SubreasonLostDroneComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSubreasonTechVisibility();
    }

    private void UpdateReasonEnabled()
    {
        // Включаем поле "Причина" только если выбран результат с Id = 2 ("Не уражено")
        if (FlyingResultComboBox.SelectedValue is int selectedId && selectedId == 2)
        {
            ReasonComboBox.IsEnabled = true;
        }
        else
        {
            ReasonComboBox.IsEnabled = false;
            ReasonComboBox.SelectedIndex = -1;
        }
    }

    private void UpdateSubreasonLostDroneVisibility()
    {
        // показываем подпричины только если выбрана причина "Втрата дрона" (Id = 2)
        if (ReasonComboBox.SelectedValue is int reasonId && reasonId == 2)
        {
            SubreasonLostDroneComboBox.IsEnabled = true;
        }
        else
        {
            SubreasonLostDroneComboBox.IsEnabled = false;
            SubreasonLostDroneComboBox.SelectedIndex = -1;
        }
    }

    private void UpdateSubreasonTechVisibility()
    {
        // включаем "Технічні помилки" только если выбран subreason "технічні помилки" в полі "Причина втрати"
        if (SubreasonLostDroneComboBox.SelectedItem is Models.SubreasonLostDrone subreason &&
            string.Equals(subreason.Name, "технічні помилки", StringComparison.OrdinalIgnoreCase))
        {
            SubreasonTechComboBox.IsEnabled = true;
        }
        else
        {
            SubreasonTechComboBox.IsEnabled = false;
            SubreasonTechComboBox.SelectedIndex = -1;
        }
    }

}

