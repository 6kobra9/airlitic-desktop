using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace AirLiticApp;

public partial class RecordEditWindow : Window
{
    private sealed class WeatherSnapshot
    {
        public string Temperature { get; init; } = string.Empty;
        public string WindDirection { get; init; } = string.Empty;
        public string WindSpeed { get; init; } = string.Empty;
        public string Precipitation { get; init; } = string.Empty;
        public string CloudCover { get; init; } = string.Empty;
        public bool IsError { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
    }

    private sealed class SquadItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class RegionItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class WeaponPartOption
    {
        // Id здесь = id конкретной строки dbo.weapon_parts (weapon_part_id)
        public int Id { get; init; }
        public int WeaponId { get; init; }
        public string WeaponName { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string FrequencyMhz { get; init; } = string.Empty;
        public string WeaponTypeName { get; init; } = string.Empty;
        public string VideoTypeName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty; // display string: weapon_name/serial/frequency/video_type
    }

    private readonly Models.Record? _record;
    private readonly Models.User _currentUser;
    private readonly List<Models.Pilot> _allPilots = new();
    private readonly Dictionary<int, List<int>> _pilotIdsBySquad = new();
    private readonly List<SquadItem> _squads = new();
    private readonly List<RegionItem> _regions = new();
    private readonly Dictionary<string, (double Lat, double Lon)> _regionCoordsCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitializing;
    private static readonly HttpClient WeatherHttpClient = new();

    public RecordEditWindow(Models.Record? record, Models.User currentUser)
    {
        _record = record;
        _currentUser = currentUser;
        InitializeComponent();
        _isInitializing = true;

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

            // Спочатку відновлюємо екіпаж (якщо знайдеться для обраного пілота), потім пілота.
            if (_record.PilotId is int pilotId && TryFindSquadIdForPilot(pilotId, out var squadId))
                SquadComboBox.SelectedValue = squadId;
            else
                SquadComboBox.SelectedIndex = -1;

            ApplyPilotFilterBySelectedSquad();
            PilotComboBox.SelectedValue = _record.PilotId;
            RegionComboBox.SelectedValue = _record.RegionId;
            // Теперь в списке выбираем конкретную строку weapon_parts,
            // поэтому selected value должен соответствовать weapon_part_id.
            WeaponComboBox.SelectedValue = _record.WeaponPartId;
            SerialNumberTextBox.Text = _record.SerialNumber ?? string.Empty;
            FlyingResultComboBox.SelectedValue = _record.FlyingResultId;
            ReasonComboBox.SelectedValue = _record.ReasonId;
            SubreasonLostDroneComboBox.SelectedValue = _record.SubreasonLostDroneId;
            SubreasonTechComboBox.SelectedValue = _record.SubreasonTechId;
            DescriptionTextBox.Text = _record.Description;
            Title = "Зміна запису";

            UpdateReasonEnabled();
            UpdateSubreasonLostDroneVisibility();
            UpdateSubreasonTechVisibility();
            _ = UpdateWeatherVisibilityAsync();
        }
        else
        {
            DatePicker.Value = DateTime.Today;
            HoursUpDown.Value = null;
            MinutesUpDown.Value = null;
            // Не выбираем экипаж по умолчанию.
            // Это важно, чтобы фильтр пилотов по экипажу не применялся автоматически.
            SquadComboBox.SelectedIndex = -1;
            ApplyPilotFilterBySelectedSquad();
            Title = "Новий запис";
            ReasonComboBox.IsEnabled = false;
            SubreasonLostDroneComboBox.IsEnabled = false;
            SubreasonLostDroneComboBox.SelectedIndex = -1;
            SubreasonTechComboBox.IsEnabled = false;
            SubreasonTechComboBox.SelectedIndex = -1;
            WeatherLabelText.Visibility = Visibility.Visible;
            WeatherPanel.Visibility = Visibility.Visible;
            SerialNumberTextBox.Text = string.Empty;
            ClearWeatherFields();
            _ = UpdateWeatherVisibilityAsync();
        }

        _isInitializing = false;
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
        int? squadId = SquadComboBox.SelectedValue as int?;
        int? regionId = RegionComboBox.SelectedValue as int?;
        int? weaponPartId = WeaponComboBox.SelectedValue as int?;
        int? flyingResultId = FlyingResultComboBox.SelectedValue as int?;
        int? reasonId = ReasonComboBox.SelectedValue as int?;
        int? subreasonLostDroneId = SubreasonLostDroneComboBox.SelectedValue as int?;
        int? subreasonTechId = SubreasonTechComboBox.SelectedValue as int?;
        var serialNumberInput = (SerialNumberTextBox.Text ?? string.Empty).Trim();

        // Если пользователь явно не выбрал эскадру (или форма загрузилась без selection),
        // попробуем вычислить squad_id по выбранному пилоту.
        if (!squadId.HasValue && pilotId.HasValue)
        {
            if (TryFindSquadIdForPilot(pilotId.Value, out var inferredSquadId) &&
                inferredSquadId > 0)
            {
                squadId = inferredSquadId;
            }
        }
        var finalDescription = MergeWeatherIntoDescription(
            DescriptionTextBox.Text.Trim(),
            ShouldAttachWeatherToDescription(),
            BuildWeatherSummaryLine());
        var weatherTemperature = string.IsNullOrWhiteSpace(TempTextBox.Text) ? null : TempTextBox.Text.Trim();
        var weatherWindDirection = string.IsNullOrWhiteSpace(WindDirectionTextBox.Text)
            ? null
            : WindDirectionTextBox.Text.Trim();
        var weatherWindSpeed = string.IsNullOrWhiteSpace(WindSpeedTextBox.Text) ? null : WindSpeedTextBox.Text.Trim();
        var weatherPrecipitation = string.IsNullOrWhiteSpace(PrecipitationTextBox.Text)
            ? null
            : PrecipitationTextBox.Text.Trim();
        var weatherCloudCover = string.IsNullOrWhiteSpace(CloudCoverTextBox.Text) ? null : CloudCoverTextBox.Text.Trim();

        if (WeatherPanel.Visibility != Visibility.Visible)
        {
            weatherTemperature = null;
            weatherWindDirection = null;
            weatherWindSpeed = null;
            weatherPrecipitation = null;
            weatherCloudCover = null;
        }

        using var db = new Data.AppDbContext();
        if (_record == null)
        {
            var newRecord = new Models.Record
            {
                Date = DatePicker.Value!.Value.Date,
                Time = time,
                PilotId = pilotId,
                SquadId = squadId,
                RegionId = regionId,
                WeaponPartId = weaponPartId,
                SerialNumber = string.IsNullOrWhiteSpace(serialNumberInput) ? null : serialNumberInput,
                FlyingResultId = flyingResultId,
                ReasonId = reasonId,
                SubreasonLostDroneId = subreasonLostDroneId,
                SubreasonTechId = subreasonTechId,
                WeatherTemperature = weatherTemperature,
                WeatherWindDirection = weatherWindDirection,
                WeatherWindSpeed = weatherWindSpeed,
                WeatherPrecipitation = weatherPrecipitation,
                WeatherCloudCover = weatherCloudCover,
                Description = finalDescription,
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
                entity.SquadId = squadId;
                entity.RegionId = regionId;
                entity.WeaponPartId = weaponPartId;
                entity.SerialNumber = string.IsNullOrWhiteSpace(serialNumberInput) ? null : serialNumberInput;
                entity.FlyingResultId = flyingResultId;
                entity.ReasonId = reasonId;
                entity.SubreasonLostDroneId = subreasonLostDroneId;
                entity.SubreasonTechId = subreasonTechId;
                entity.WeatherTemperature = weatherTemperature;
                entity.WeatherWindDirection = weatherWindDirection;
                entity.WeatherWindSpeed = weatherWindSpeed;
                entity.WeatherPrecipitation = weatherPrecipitation;
                entity.WeatherCloudCover = weatherCloudCover;
                entity.Description = finalDescription;
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
        _allPilots.Clear();
        _allPilots.AddRange(db.Pilots
            .OrderBy(p => p.Name)
            .ToList());
        PilotComboBox.ItemsSource = _allPilots;

        LoadSquadsAndPilotMap(db);
        SquadComboBox.ItemsSource = _squads;
        SquadComboBox.IsEnabled = _squads.Count > 0;
        LoadRegions(db);
        RegionComboBox.ItemsSource = _regions;
        RegionComboBox.IsEnabled = _regions.Count > 0;
        if (_regions.Count > 0)
        {
            var defaultRegion = _regions.FirstOrDefault(r =>
                string.Equals((r.Name ?? string.Empty).Trim(), "Степногорск", StringComparison.OrdinalIgnoreCase) ||
                string.Equals((r.Name ?? string.Empty).Trim(), "Степногірськ", StringComparison.OrdinalIgnoreCase));
            if (defaultRegion != null)
                RegionComboBox.SelectedValue = defaultRegion.Id;
            else
                RegionComboBox.SelectedIndex = 0;
        }

        WeaponComboBox.ItemsSource = LoadWeaponPartOptions(db);
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

    private System.Collections.Generic.List<WeaponPartOption> LoadWeaponPartOptions(Data.AppDbContext db)
    {
        var options = new System.Collections.Generic.List<WeaponPartOption>();

        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            // Каждая строка ComboBox соответствует конкретной записи dbo.weapon_parts.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
select
    wp.id as weapon_part_id,
    w.id as weapon_id,
    coalesce(
        nullif(ltrim(rtrim(w.name)), ''),
        nullif(ltrim(rtrim(w.code)), '')
    ) as weapon_name,
    wt.name as weapon_type_name,
    wp.serial_number,
    wp.frequency_mhz,
    vt.name as video_type_name
from weapon_parts wp
join weapon w on w.id = wp.weapon_id
left join weapon_type wt on wt.id = w.type_id
left join video_type vt on vt.id = wp.video_type_id
order by
    weapon_name,
    wp.serial_number,
    wp.frequency_mhz,
    wp.id;";

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var weaponPartId = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                var weaponId = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                if (weaponPartId <= 0 || weaponId <= 0)
                    continue;

                var weaponName = rd.IsDBNull(2) ? string.Empty : rd.GetString(2);
                var weaponTypeName = rd.IsDBNull(3) ? string.Empty : rd.GetString(3);
                var serial = rd.IsDBNull(4) ? string.Empty : rd.GetString(4);
                var frequencyMhz = rd.IsDBNull(5) ? (decimal?)null : rd.GetDecimal(5);
                var videoTypeName = rd.IsDBNull(6) ? string.Empty : rd.GetString(6);

                var freqText = frequencyMhz.HasValue
                    ? frequencyMhz.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    : string.Empty;

                options.Add(new WeaponPartOption
                {
                    Id = weaponPartId,
                    WeaponId = weaponId,
                    WeaponName = weaponName,
                    SerialNumber = serial,
                    FrequencyMhz = freqText,
                    WeaponTypeName = weaponTypeName,
                    VideoTypeName = videoTypeName,
                    Name = $"{weaponName}/{serial}/{freqText}/{videoTypeName}"
                });
            }
        }
        catch
        {
            // Если по какой-то причине weapon_parts/video_type еще не доступны,
            // не ломаем форму: покажем пустой список.
        }

        return options;
    }

    private void LoadSquadsAndPilotMap(Data.AppDbContext db)
    {
        _squads.Clear();
        _pilotIdsBySquad.Clear();

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        try
        {
            // Таблиця екіпажів: squad(id, name)
            using (var squadsCmd = conn.CreateCommand())
            {
                squadsCmd.CommandText = "select id, name from squad order by name";
                using var reader = squadsCmd.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (id > 0)
                        _squads.Add(new SquadItem { Id = id, Name = name });
                }
            }

            // Зв'язка пілот-екіпаж. Підтримуємо імена: squad_pilot / pilot_squad / squad_map.
            var mapTable = DetectPilotSquadMapTable(conn);
            if (mapTable == null)
                return;

            using var mapCmd = conn.CreateCommand();
            mapCmd.CommandText = $"select pilot_id, squad_id from {mapTable}";
            using var mapReader = mapCmd.ExecuteReader();
            while (mapReader.Read())
            {
                if (mapReader.IsDBNull(0) || mapReader.IsDBNull(1))
                    continue;

                var pilotId = mapReader.GetInt32(0);
                var squadId = mapReader.GetInt32(1);
                if (!_pilotIdsBySquad.TryGetValue(squadId, out var list))
                {
                    list = new List<int>();
                    _pilotIdsBySquad[squadId] = list;
                }

                if (!list.Contains(pilotId))
                    list.Add(pilotId);
            }
        }
        catch
        {
            // Якщо таблиці екіпажів/зв'язки відсутні, форма продовжує працювати без фільтра екіпажів.
            _squads.Clear();
            _pilotIdsBySquad.Clear();
        }
    }

    private void LoadRegions(Data.AppDbContext db)
    {
        _regions.Clear();
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select id, name from region where name is not null order by name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                var name = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                    _regions.Add(new RegionItem { Id = id, Name = name });
            }
        }
        catch
        {
            // Якщо таблиця region відсутня, просто залишаємо порожній список.
        }
    }

    private static string? DetectPilotSquadMapTable(System.Data.Common.DbConnection conn)
    {
        var candidates = new[] { "squad_rel", "squad_pilot", "pilot_squad", "squad_map" };
        foreach (var table in candidates)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $@"
if exists (
    select 1
    from information_schema.columns
    where table_name = '{table}'
      and column_name in ('pilot_id','squad_id')
    group by table_name
    having count(distinct column_name) = 2
)
    select 1
else
    select 0";
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (exists == 1)
                return table;
        }

        return null;
    }

    private bool TryFindSquadIdForPilot(int pilotId, out int squadId)
    {
        foreach (var kv in _pilotIdsBySquad)
        {
            if (kv.Value.Contains(pilotId))
            {
                squadId = kv.Key;
                return true;
            }
        }

        squadId = 0;
        return false;
    }

    private void ApplyPilotFilterBySelectedSquad()
    {
        var selectedPilotId = PilotComboBox.SelectedValue as int?;

        IEnumerable<Models.Pilot> source = _allPilots;
        if (SquadComboBox.SelectedValue is int squadId &&
            _pilotIdsBySquad.TryGetValue(squadId, out var pilotIds) &&
            pilotIds != null)
        {
            source = _allPilots.Where(p => pilotIds.Contains(p.Id));
        }

        var filtered = source.OrderBy(p => p.Name).ToList();
        PilotComboBox.ItemsSource = filtered;

        if (selectedPilotId.HasValue && filtered.Any(p => p.Id == selectedPilotId.Value))
            PilotComboBox.SelectedValue = selectedPilotId.Value;
        else
            PilotComboBox.SelectedIndex = -1;
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
        _ = UpdateWeatherVisibilityAsync();
    }

    private void SquadComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing)
            return;
        ApplyPilotFilterBySelectedSquad();
    }

    private void WeaponComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WeaponComboBox.SelectedItem is not WeaponPartOption selected)
            return;
        if (!string.IsNullOrWhiteSpace(SerialNumberTextBox.Text))
            return;

        SerialNumberTextBox.Text = selected.SerialNumber;
    }

    private void RegionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing || WeatherPanel.Visibility != Visibility.Visible)
            return;
        _ = UpdateWeatherVisibilityAsync();
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

    private bool ShouldAttachWeatherToDescription() =>
        WeatherPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(BuildWeatherSummaryLine());

    private static string MergeWeatherIntoDescription(string description, bool withWeather, string weatherText)
    {
        var lines = description
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(x => !x.TrimStart().StartsWith("[Погода]", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var clean = string.Join(Environment.NewLine, lines).Trim();

        if (!withWeather)
            return clean;

        var weatherLine = "[Погода] " + weatherText.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? weatherLine
            : clean + Environment.NewLine + weatherLine;
    }

    private async Task UpdateWeatherVisibilityAsync()
    {
        // Погода отображается всегда и обновляется по выбранной дате/времени,
        // независимо от выбора "Причина втрати" / subreason.
        WeatherLabelText.Visibility = Visibility.Visible;
        WeatherPanel.Visibility = Visibility.Visible;
        SetWeatherLoading();

        var weatherAt = GetSelectedWeatherDateTime();
        var regionCoords = await ResolveSelectedRegionCoordinatesAsync();
        WeatherSnapshot weather;
        if (!regionCoords.HasValue)
        {
            weather = new WeatherSnapshot
            {
                IsError = true,
                ErrorMessage = "Не вдалося визначити координати для обраного району"
            };
        }
        else
        {
            weather = await FetchWeatherAsync(weatherAt, regionCoords.Value.Lat, regionCoords.Value.Lon);
        }
        FillWeatherFields(weather);
    }

    private DateTime GetSelectedWeatherDateTime()
    {
        var date = DatePicker.Value?.Date ?? DateTime.Today;
        var hh = Math.Clamp(HoursUpDown.Value ?? 12, 0, 23);
        var mm = Math.Clamp(MinutesUpDown.Value ?? 0, 0, 59);
        return new DateTime(date.Year, date.Month, date.Day, hh, mm, 0, DateTimeKind.Local);
    }

    private void WeatherDateOrTime_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || WeatherPanel.Visibility != Visibility.Visible)
            return;
        _ = UpdateWeatherVisibilityAsync();
    }

    private void SetWeatherLoading()
    {
        TempTextBox.Text = "Температура: завантаження...";
        WindDirectionTextBox.Text = "Напрямок вітру: завантаження...";
        WindSpeedTextBox.Text = "Швидкість вітру: завантаження...";
        PrecipitationTextBox.Text = "Опади: завантаження...";
        CloudCoverTextBox.Text = "Хмарність: завантаження...";
    }

    private void ClearWeatherFields()
    {
        TempTextBox.Text = string.Empty;
        WindDirectionTextBox.Text = string.Empty;
        WindSpeedTextBox.Text = string.Empty;
        PrecipitationTextBox.Text = string.Empty;
        CloudCoverTextBox.Text = string.Empty;
    }

    private void FillWeatherFields(WeatherSnapshot weather)
    {
        if (weather.IsError)
        {
            TempTextBox.Text = weather.ErrorMessage;
            WindDirectionTextBox.Text = string.Empty;
            WindSpeedTextBox.Text = string.Empty;
            PrecipitationTextBox.Text = string.Empty;
            CloudCoverTextBox.Text = string.Empty;
            return;
        }

        TempTextBox.Text = weather.Temperature;
        WindDirectionTextBox.Text = weather.WindDirection;
        WindSpeedTextBox.Text = weather.WindSpeed;
        PrecipitationTextBox.Text = weather.Precipitation;
        CloudCoverTextBox.Text = weather.CloudCover;
    }

    private string BuildWeatherSummaryLine()
    {
        var parts = new[]
        {
            TempTextBox.Text,
            WindDirectionTextBox.Text,
            WindSpeedTextBox.Text,
            PrecipitationTextBox.Text,
            CloudCoverTextBox.Text
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();
        return string.Join("; ", parts);
    }

    private async Task<(double Lat, double Lon)?> ResolveSelectedRegionCoordinatesAsync()
    {
        if (RegionComboBox.SelectedItem is not RegionItem region || string.IsNullOrWhiteSpace(region.Name))
            return null;

        var regionName = region.Name.Trim();
        if (_regionCoordsCache.TryGetValue(regionName, out var cached))
            return cached;

        try
        {
            var url =
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(regionName)}&count=1&language=uk&format=json";
            using var response = await WeatherHttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            if (!first.TryGetProperty("latitude", out var latEl) ||
                !first.TryGetProperty("longitude", out var lonEl) ||
                latEl.ValueKind != JsonValueKind.Number ||
                lonEl.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var coords = (Lat: latEl.GetDouble(), Lon: lonEl.GetDouble());
            _regionCoordsCache[regionName] = coords;
            return coords;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<WeatherSnapshot> FetchWeatherAsync(DateTime localDateTime, double lat, double lon)
    {
        try
        {
            var date = localDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var url =
                $"https://archive-api.open-meteo.com/v1/archive?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&start_date={date}&end_date={date}&hourly=temperature_2m,wind_speed_10m,wind_direction_10m,precipitation,cloud_cover,weather_code&timezone=Europe%2FKiev";

            using var response = await WeatherHttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hourly", out var hourly))
                return new WeatherSnapshot { IsError = true, ErrorMessage = "Погоду не знайдено" };

            if (!hourly.TryGetProperty("time", out var times))
                return new WeatherSnapshot { IsError = true, ErrorMessage = "Погоду не знайдено" };

            var targetHour = localDateTime.Minute >= 30 ? localDateTime.Hour + 1 : localDateTime.Hour;
            if (targetHour >= 24) targetHour = 23;
            var targetKey = $"{date}T{targetHour:00}:00";

            var idx = -1;
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                if (string.Equals(times[i].GetString(), targetKey, StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0 && times.GetArrayLength() > 0)
                idx = Math.Min(times.GetArrayLength() - 1, targetHour);
            if (idx < 0)
                return new WeatherSnapshot { IsError = true, ErrorMessage = "Погоду не знайдено" };

            static double GetDoubleAt(JsonElement parent, string prop, int i)
            {
                if (!parent.TryGetProperty(prop, out var arr)) return 0d;
                if (arr.ValueKind != JsonValueKind.Array || i < 0 || i >= arr.GetArrayLength()) return 0d;
                var el = arr[i];
                return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : 0d;
            }
            static int GetIntAt(JsonElement parent, string prop, int i)
            {
                if (!parent.TryGetProperty(prop, out var arr)) return -1;
                if (arr.ValueKind != JsonValueKind.Array || i < 0 || i >= arr.GetArrayLength()) return -1;
                var el = arr[i];
                return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : -1;
            }

            var temp = GetDoubleAt(hourly, "temperature_2m", idx);
            var windSpeed = GetDoubleAt(hourly, "wind_speed_10m", idx);
            var windDirection = GetDoubleAt(hourly, "wind_direction_10m", idx);
            var precipitation = GetDoubleAt(hourly, "precipitation", idx);
            var cloudCover = GetDoubleAt(hourly, "cloud_cover", idx);
            var code = GetIntAt(hourly, "weather_code", idx);
            var weatherKind = MapWeatherCode(code);

            return new WeatherSnapshot
            {
                Temperature = string.Format(CultureInfo.CurrentCulture, "Температура: {0:N1}°C", temp),
                WindDirection = $"Напрямок вітру: {ToCompass8(windDirection)} ({windDirection:N0}°)",
                WindSpeed = string.Format(CultureInfo.CurrentCulture, "Швидкість вітру: {0:N1} м/с", windSpeed),
                Precipitation = string.Format(CultureInfo.CurrentCulture, "Опади: {0:N1} мм", precipitation),
                CloudCover = string.Format(CultureInfo.CurrentCulture, "Хмарність: {0:N0}% ({1})", cloudCover, weatherKind)
            };
        }
        catch
        {
            return new WeatherSnapshot
            {
                IsError = true,
                ErrorMessage = "Не вдалося автоматично отримати погоду"
            };
        }
    }

    private static string ToCompass8(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            return "н/д";
        var d = (degrees % 360 + 360) % 360;
        return d switch
        {
            >= 337.5 or < 22.5 => "Пн",
            < 67.5 => "ПнСх",
            < 112.5 => "Сх",
            < 157.5 => "ПдСх",
            < 202.5 => "Пд",
            < 247.5 => "ПдЗх",
            < 292.5 => "Зх",
            _ => "ПнЗх"
        };
    }

    private static string MapWeatherCode(int code) =>
        code switch
        {
            0 => "Ясно",
            1 or 2 => "Мінлива хмарність",
            3 => "Похмуро",
            45 or 48 => "Туман",
            51 or 53 or 55 or 56 or 57 => "Мряка",
            61 or 63 or 65 or 66 or 67 => "Дощ",
            71 or 73 or 75 or 77 => "Сніг",
            80 or 81 or 82 => "Злива",
            85 or 86 => "Снігопад",
            95 or 96 or 99 => "Гроза",
            _ => "Невизначена погода"
        };

}

