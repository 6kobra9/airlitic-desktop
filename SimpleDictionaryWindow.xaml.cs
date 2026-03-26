using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace AirLiticApp;

public partial class SimpleDictionaryWindow : Window
{
    private readonly SimpleDictionaryKind _kind;
    private readonly ObservableCollection<Row> _rows = new();
    private readonly ObservableCollection<WeaponEntryRow> _weaponEntries = new();
    private readonly ObservableCollection<SquadEntryRow> _squadEntries = new();
    public ObservableCollection<WeaponOption> WeaponNameOptions { get; } = new();
    public ObservableCollection<string> WeaponTypeOptions { get; } = new();
    public ObservableCollection<string> SerialNumberOptions { get; } = new();
    public ObservableCollection<string> VideoTypeOptions { get; } = new();
    public ObservableCollection<string> SquadNameOptions { get; } = new();

    public sealed class WeaponOption
    {
        public int Id { get; init; }
        public string WeaponName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    public SimpleDictionaryWindow(SimpleDictionaryKind kind)
    {
        _kind = kind;
        InitializeComponent();
        DataContext = this;
        WeaponsPart1Panel.Visibility = _kind == SimpleDictionaryKind.Weapons ? Visibility.Visible : Visibility.Collapsed;
        PilotsPart1SquadsPanel.Visibility = _kind == SimpleDictionaryKind.Pilots ? Visibility.Visible : Visibility.Collapsed;

        var isWeapons = _kind == SimpleDictionaryKind.Weapons;
        WeaponNameColumn.Visibility = isWeapons ? Visibility.Visible : Visibility.Collapsed;
        var showNameColumn = _kind != SimpleDictionaryKind.Weapons;
        PilotNameColumn.Visibility = showNameColumn ? Visibility.Visible : Visibility.Collapsed;
        PilotNameColumn.Header = _kind == SimpleDictionaryKind.Pilots ? "Пілот" : "Назва";

        SquadColumn.Visibility = _kind == SimpleDictionaryKind.Pilots ? Visibility.Visible : Visibility.Collapsed;
        SerialNumberColumn.Visibility = isWeapons ? Visibility.Visible : Visibility.Collapsed;
        VideoTypeColumn.Visibility = isWeapons ? Visibility.Visible : Visibility.Collapsed;
        FrequencyColumn.Visibility = isWeapons ? Visibility.Visible : Visibility.Collapsed;
        ItemsGrid.ItemsSource = _rows;
        WeaponEntriesGrid.ItemsSource = _weaponEntries;
        SquadEntriesGrid.ItemsSource = _squadEntries;
        LoadData();
    }

    private void LoadData()
    {
        _rows.Clear();
        _weaponEntries.Clear();
        _squadEntries.Clear();
        SquadNameOptions.Clear();
        try
        {
            using var db = new Data.AppDbContext();

            switch (_kind)
            {
                case SimpleDictionaryKind.Pilots:
                    Title = "Пілоти";
                    LoadSquadEntries(db);
                    RefreshSquadNameOptions();
                    var pilotSquads = LoadPilotSquads(db);
                    foreach (var x in db.Pilots.OrderBy(p => p.Name))
                    {
                        pilotSquads.TryGetValue(x.Id, out var squadName);
                        _rows.Add(new Row { Id = x.Id, Name = x.Name, SquadName = squadName ?? string.Empty });
                    }
                    break;
                case SimpleDictionaryKind.Weapons:
                    Title = "Засоби";
                    foreach (var w in db.Weapons.OrderBy(x => x.Name))
                        _weaponEntries.Add(new WeaponEntryRow { Id = w.Id, Name = w.Name ?? string.Empty });
                    foreach (var x in LoadWeaponsWithMeta(db))
                        _rows.Add(x);
                    LoadWeaponNameOptionsFromDb(db);
                    LoadWeaponTypeOptionsFromDb(db);
                    LoadVideoTypeOptionsFromDb(db);
                    RefreshWeaponColumnOptions();
                    break;
                case SimpleDictionaryKind.SubreasonLostDrone:
                    Title = "Причина втрати";
                    foreach (var x in db.SubreasonLostDrones.OrderBy(s => s.Name))
                        _rows.Add(new Row { Id = x.Id, Name = x.Name });
                    break;
                case SimpleDictionaryKind.SubreasonTech:
                    Title = "Технічні проблеми";
                    foreach (var x in db.SubreasonTeches.OrderBy(s => s.Name))
                        _rows.Add(new Row { Id = x.Id, Name = x.Name });
                    break;
            }
        }
        catch
        {
            MessageBox.Show(Data.DbHealth.GetUnavailableMessage(), "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
        }
    }

    private void LoadSquadEntries(Data.AppDbContext db)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            if (!TableExists(conn, "squad"))
                return;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select id, name from squad order by name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                var name = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                    _squadEntries.Add(new SquadEntryRow { Id = id, Name = name });
            }
        }
        catch
        {
            // ignore
        }
    }

    private void RefreshSquadNameOptions()
    {
        SquadNameOptions.Clear();
        SquadNameOptions.Add(string.Empty);

        foreach (var v in _squadEntries
                     .Select(r => (r.Name ?? string.Empty).Trim())
                     .Where(v => !string.IsNullOrWhiteSpace(v))
                     .Distinct(System.StringComparer.OrdinalIgnoreCase)
                     .OrderBy(v => v, System.StringComparer.CurrentCultureIgnoreCase))
        {
            if (!SquadNameOptions.Any(x =>
                    string.Equals(x, v, System.StringComparison.OrdinalIgnoreCase)))
                SquadNameOptions.Add(v);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var baseWeaponId = 0;
        var baseWeaponName = string.Empty;
        var baseWeaponTypeName = string.Empty;

        // Копирование только для "Засобів", чтобы новая строка сохраняла weapon_id.
        // Для "Пілотів" и других справочников новая строка должна быть пустой.
        if (_kind == SimpleDictionaryKind.Weapons)
        {
            var baseRow = ItemsGrid.SelectedItem as Row;
            baseWeaponId = baseRow?.Id ?? 0;
            baseWeaponName = baseRow?.Name ?? string.Empty;
            baseWeaponTypeName = baseRow?.WeaponTypeName ?? string.Empty;
        }

        _rows.Add(new Row
        {
            Id = baseWeaponId,
            Name = baseWeaponName,
            SquadName = "",
            WeaponTypeName = baseWeaponTypeName,
            SerialNumber = "",
            FrequencyMhz = "",
            VideoTypeName = ""
        });
        ItemsGrid.SelectedIndex = _rows.Count - 1;
        ItemsGrid.ScrollIntoView(ItemsGrid.SelectedItem);
        RefreshWeaponColumnOptions();
    }

    private void AddWeaponEntry_Click(object sender, RoutedEventArgs e)
    {
        _weaponEntries.Add(new WeaponEntryRow { Id = 0, Name = "" });
        WeaponEntriesGrid.SelectedIndex = _weaponEntries.Count - 1;
        WeaponEntriesGrid.ScrollIntoView(WeaponEntriesGrid.SelectedItem);
    }

    private void DeleteWeaponEntry_Click(object sender, RoutedEventArgs e)
    {
        if (WeaponEntriesGrid.SelectedItem is not WeaponEntryRow row)
            return;

        _weaponEntries.Remove(row);
    }

    private void SaveWeaponEntries_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WeaponEntriesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            WeaponEntriesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        }
        catch
        {
            // ignore
        }

        if (!Data.DbHealth.IsDatabaseAvailable())
        {
            MessageBox.Show(Data.DbHealth.GetUnavailableMessage(), "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var cleaned = _weaponEntries
            .Select(r => new WeaponEntryRow
            {
                Id = r.Id,
                Name = (r.Name ?? "").Trim()
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToList();

        try
        {
            using var db = new Data.AppDbContext();

            var existing = db.Weapons.ToDictionary(x => x.Id, x => x);
            var existingIds = existing.Keys.ToHashSet();

            foreach (var row in cleaned)
            {
                if (row.Id <= 0)
                {
                    db.Weapons.Add(new Models.Weapon { Name = row.Name });
                    continue;
                }

                if (!existingIds.Contains(row.Id))
                    continue;

                var entity = existing[row.Id];
                if (!string.Equals(entity.Name ?? string.Empty, row.Name, System.StringComparison.Ordinal))
                    entity.Name = row.Name;

                existingIds.Remove(row.Id);
            }

            foreach (var id in existingIds)
            {
                db.Weapons.Remove(existing[id]);
            }

            db.SaveChanges();
            LoadData();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Помилка збереження засобів: {ex.Message}", "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AddSquadEntry_Click(object sender, RoutedEventArgs e)
    {
        _squadEntries.Add(new SquadEntryRow { Id = 0, Name = "" });
        SquadEntriesGrid.SelectedIndex = _squadEntries.Count - 1;
        SquadEntriesGrid.ScrollIntoView(SquadEntriesGrid.SelectedItem);
        RefreshSquadNameOptions();
    }

    private void DeleteSquadEntry_Click(object sender, RoutedEventArgs e)
    {
        if (SquadEntriesGrid.SelectedItem is not SquadEntryRow row)
            return;

        _squadEntries.Remove(row);
        RefreshSquadNameOptions();
    }

    private void SaveSquadEntries_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SquadEntriesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            SquadEntriesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        }
        catch
        {
            // ignore
        }

        if (!Data.DbHealth.IsDatabaseAvailable())
        {
            MessageBox.Show(Data.DbHealth.GetUnavailableMessage(), "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var cleaned = _squadEntries
            .Select(r => new SquadEntryRow { Id = r.Id, Name = (r.Name ?? "").Trim() })
            .Where(r => r.Id > 0 || !string.IsNullOrWhiteSpace(r.Name))
            .ToList();

        try
        {
            using var db = new Data.AppDbContext();

            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                EnsureSquadTable(conn, tx);
                var mapTable = DetectPilotSquadMapTable(conn, tx);

                var existing = new Dictionary<int, string?>();
                using (var sel = conn.CreateCommand())
                {
                    sel.Transaction = tx;
                    sel.CommandText = "select id, name from squad";
                    using var rd = sel.ExecuteReader();
                    while (rd.Read())
                    {
                        var id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        var name = rd.IsDBNull(1) ? null : rd.GetString(1);
                        if (id > 0)
                            existing[id] = name;
                    }
                }

                var desiredIds = cleaned
                    .Where(r => r.Id > 0)
                    .Select(r => r.Id)
                    .ToHashSet();

                // insert/update
                foreach (var row in cleaned)
                {
                    if (string.IsNullOrWhiteSpace(row.Name))
                        continue;

                    if (row.Id <= 0)
                    {
                        using var ins = conn.CreateCommand();
                        ins.Transaction = tx;
                        ins.CommandText = "insert into squad(name) values(@n)";
                        var p = ins.CreateParameter();
                        p.ParameterName = "@n";
                        p.Value = row.Name;
                        ins.Parameters.Add(p);
                        ins.ExecuteNonQuery();
                    }
                    else if (existing.TryGetValue(row.Id, out var oldName))
                    {
                        if (!string.Equals((oldName ?? "").Trim(), row.Name, System.StringComparison.Ordinal))
                        {
                            using var upd = conn.CreateCommand();
                            upd.Transaction = tx;
                            upd.CommandText = "update squad set name=@n where id=@id";
                            var pN = upd.CreateParameter();
                            pN.ParameterName = "@n";
                            pN.Value = row.Name;
                            upd.Parameters.Add(pN);

                            var pId = upd.CreateParameter();
                            pId.ParameterName = "@id";
                            pId.Value = row.Id;
                            upd.Parameters.Add(pId);

                            upd.ExecuteNonQuery();
                        }
                    }
                }

                // delete removed squads
                foreach (var id in existing.Keys)
                {
                    if (desiredIds.Contains(id))
                        continue;

                    if (mapTable != null)
                    {
                        using var delMap = conn.CreateCommand();
                        delMap.Transaction = tx;
                        delMap.CommandText = $"delete from {mapTable} where squad_id=@sid";
                        var pSid = delMap.CreateParameter();
                        pSid.ParameterName = "@sid";
                        pSid.Value = id;
                        delMap.Parameters.Add(pSid);
                        delMap.ExecuteNonQuery();
                    }

                    using var delSquad = conn.CreateCommand();
                    delSquad.Transaction = tx;
                    delSquad.CommandText = "delete from squad where id=@id";
                    var pId2 = delSquad.CreateParameter();
                    pId2.ParameterName = "@id";
                    pId2.Value = id;
                    delSquad.Parameters.Add(pId2);
                    delSquad.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            LoadData();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Помилка збереження экипажей: {ex.Message}", "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Row row)
            return;

        _rows.Remove(row);
        RefreshWeaponColumnOptions();
    }

    private void ItemsGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (_kind != SimpleDictionaryKind.Weapons)
            return;

        Dispatcher.BeginInvoke(new System.Action(RefreshWeaponColumnOptions),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void WeaponNameComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_kind != SimpleDictionaryKind.Weapons)
            return;

        if (sender is not System.Windows.Controls.ComboBox cb)
            return;
        if (cb.SelectedItem is not WeaponOption opt)
            return;
        if (cb.DataContext is not Row row)
            return;

        // ComboBox меняет Id через SelectedValue, но Name нужно синхронизировать вручную.
        row.Id = opt.Id;
        row.Name = opt.WeaponName;
    }

    private void RefreshWeaponColumnOptions()
    {
        if (_kind != SimpleDictionaryKind.Weapons)
            return;

        foreach (var v in _rows
                     .Select(r => (r.WeaponTypeName ?? string.Empty).Trim())
                     .Where(v => !string.IsNullOrWhiteSpace(v))
                     .Distinct(System.StringComparer.OrdinalIgnoreCase)
                     .OrderBy(v => v, System.StringComparer.CurrentCultureIgnoreCase))
        {
            if (!WeaponTypeOptions.Any(x => string.Equals(x, v, System.StringComparison.OrdinalIgnoreCase)))
                WeaponTypeOptions.Add(v);
        }

        SerialNumberOptions.Clear();
        foreach (var v in _rows
                     .Select(r => (r.SerialNumber ?? string.Empty).Trim())
                     .Where(v => !string.IsNullOrWhiteSpace(v))
                     .Distinct(System.StringComparer.OrdinalIgnoreCase)
                     .OrderBy(v => v, System.StringComparer.CurrentCultureIgnoreCase))
        {
            SerialNumberOptions.Add(v);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
       
        try
        {
            ItemsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            ItemsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        }
        catch
        {
            // не критично
        }

        if (!Data.DbHealth.IsDatabaseAvailable())
        {
            MessageBox.Show(Data.DbHealth.GetUnavailableMessage(), "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var cleaned = _rows
            .Select(r => new Row
            {
                Id = r.Id,
                Name = (r.Name ?? "").Trim(),
                SquadName = (r.SquadName ?? "").Trim(),
                WeaponTypeName = (r.WeaponTypeName ?? "").Trim(),
                SerialNumber = (r.SerialNumber ?? "").Trim(),
                FrequencyMhz = (r.FrequencyMhz ?? "").Trim(),
                VideoTypeName = (r.VideoTypeName ?? "").Trim()
            })
            // Для существующих записей (Id>0) допускаем пустое Name,
            // иначе можно потерять метаданные в `weapon_parts`, когда `weapon.name` пустой.
            .Where(r => r.Id > 0 || !string.IsNullOrWhiteSpace(r.Name))
            .ToList();

        try
        {
            using var db = new Data.AppDbContext();

            switch (_kind)
            {
                case SimpleDictionaryKind.Pilots:
                    SavePilots(db, cleaned);
                    break;
                case SimpleDictionaryKind.Weapons:
                    SaveWeapons(db, cleaned);
                    break;
                case SimpleDictionaryKind.SubreasonLostDrone:
                    SaveSubreasonLostDrone(db, cleaned);
                    break;
                case SimpleDictionaryKind.SubreasonTech:
                    SaveSubreasonTech(db, cleaned);
                    break;
            }

            db.SaveChanges();
            LoadData();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Помилка збереження довідника: {ex.Message}", "Помилка", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void SavePilots(Data.AppDbContext db, System.Collections.Generic.List<Row> rows)
    {
        var existing = db.Pilots.ToDictionary(x => x.Id);
        ApplySimpleEdits(
            rows,
            existing.Keys.ToHashSet(),
            id => existing[id].Name,
            (id, name) => existing[id].Name = name,
            name => db.Pilots.Add(new Models.Pilot { Name = name }),
            id => db.Pilots.Remove(existing[id]));

        // Нужно получить ID для новодобавленных пилотов перед сохранением связей пилот-экипаж.
        db.SaveChanges();
        SavePilotSquads(db, rows);
    }

    private static void SaveWeapons(Data.AppDbContext db, System.Collections.Generic.List<Row> rows)
    {
        // Важно: не меняем записи в таблице `weapon` (не добавляем/не удаляем).
        // Сохраняем только связи/метаданные в `weapon_parts` (и при необходимости обновляем `weapon.type_id`).
        SaveWeaponMetadata(db, rows);
    }

    private static void SaveSubreasonLostDrone(Data.AppDbContext db, System.Collections.Generic.List<Row> rows)
    {
        var existing = db.SubreasonLostDrones.ToDictionary(x => x.Id);
        ApplySimpleEdits(
            rows,
            existing.Keys.ToHashSet(),
            id => existing[id].Name,
            (id, name) => existing[id].Name = name,
            name => db.SubreasonLostDrones.Add(new Models.SubreasonLostDrone { Name = name }),
            id => db.SubreasonLostDrones.Remove(existing[id]));
    }

    private static void SaveSubreasonTech(Data.AppDbContext db, System.Collections.Generic.List<Row> rows)
    {
        var existing = db.SubreasonTeches.ToDictionary(x => x.Id);
        ApplySimpleEdits(
            rows,
            existing.Keys.ToHashSet(),
            id => existing[id].Name,
            (id, name) => existing[id].Name = name,
            name => db.SubreasonTeches.Add(new Models.SubreasonTech { Name = name }),
            id => db.SubreasonTeches.Remove(existing[id]));
    }

    private static void ApplySimpleEdits(
        System.Collections.Generic.List<Row> desired,
        System.Collections.Generic.HashSet<int> existingIds,
        System.Func<int, string> getName,
        System.Action<int, string> setName,
        System.Action<string> addNew,
        System.Action<int> deleteById)
    {
        foreach (var row in desired)
        {
            if (row.Id <= 0)
            {
                addNew(row.Name);
                continue;
            }

            if (!existingIds.Contains(row.Id))
                continue;

            if (getName(row.Id) != row.Name)
                setName(row.Id, row.Name);

            existingIds.Remove(row.Id);
        }

        foreach (var id in existingIds)
        {
            deleteById(id);
        }
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SquadName { get; set; } = string.Empty;
        public string WeaponTypeName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string FrequencyMhz { get; set; } = string.Empty;
        public string VideoTypeName { get; set; } = string.Empty;
    }

    private sealed class WeaponEntryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SquadEntryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static List<Row> LoadWeaponsWithMeta(Data.AppDbContext db)
    {
        var rows = new List<Row>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        EnsureWeaponMetaTables(conn, tx: null);

        using var cmd = conn.CreateCommand();
        // Важно: на 1 weapon_id может быть много weapon_parts с разными serial/video/frequency,
        // поэтому загружаем все записи weapon_parts, а не top(1).
        cmd.CommandText = @"
select
    w.id,
    coalesce(
        nullif(ltrim(rtrim(w.name)), ''),
        nullif(ltrim(rtrim(w.code)), '')
    ) as weapon_name,
    wt.name as weapon_type_name,
    wp.serial_number,
    wp.frequency_mhz,
    vt.name as video_type_name
from weapon w
left join weapon_type wt on wt.id = w.type_id
inner join weapon_parts wp on wp.weapon_id = w.id
left join video_type vt on vt.id = wp.video_type_id
where wp.serial_number is not null
   or wp.video_type_id is not null
   or wp.frequency_mhz is not null
order by weapon_name, wp.id";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            rows.Add(new Row
            {
                Id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                Name = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                WeaponTypeName = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                SerialNumber = rd.IsDBNull(3) ? string.Empty : rd.GetString(3),
                FrequencyMhz = rd.IsDBNull(4) ? string.Empty : rd.GetDecimal(4).ToString(),
                VideoTypeName = rd.IsDBNull(5) ? string.Empty : rd.GetString(5)
            });
        }

        return rows;
    }

    private static void SaveWeaponMetadata(Data.AppDbContext db, List<Row> rows)
    {
        var weaponsByName = db.Weapons
            .AsNoTracking()
            .OrderBy(w => w.Id)
            .ToList()
            .GroupBy(w => (w.Name ?? string.Empty).Trim(), System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, System.StringComparer.OrdinalIgnoreCase);

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var tx = conn.BeginTransaction();
        try
        {
            EnsureWeaponMetaTables(conn, tx);

            // Перед полной пересборкой weapon_parts снимаем ссылки из results,
            // иначе FK results.weapon_part_id -> weapon_parts.id блокирует delete.
            using (var clearRefs = conn.CreateCommand())
            {
                clearRefs.Transaction = tx;
                clearRefs.CommandText = @"
if col_length('dbo.results','weapon_part_id') is not null
begin
    update dbo.results
    set weapon_part_id = null
    where weapon_part_id is not null;
end";
                clearRefs.ExecuteNonQuery();
            }

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "delete from weapon_parts";
                del.ExecuteNonQuery();
            }

            foreach (var row in rows)
            {
                int weaponId = row.Id;
                if (weaponId <= 0)
                {
                    if (!weaponsByName.TryGetValue(row.Name, out weaponId))
                        continue;
                }

                var hasPartsMeta =
                    !string.IsNullOrWhiteSpace(row.SerialNumber) ||
                    !string.IsNullOrWhiteSpace(row.FrequencyMhz) ||
                    !string.IsNullOrWhiteSpace(row.VideoTypeName);

                var hasTypeMeta = !string.IsNullOrWhiteSpace(row.WeaponTypeName);

                if (!hasPartsMeta && !hasTypeMeta)
                    continue;

                int? weaponTypeId = null;
                int? videoTypeId = null;

                if (hasTypeMeta)
                    weaponTypeId = EnsureLookupItem(conn, tx, "weapon_type", row.WeaponTypeName);

                if (!string.IsNullOrWhiteSpace(row.VideoTypeName))
                    videoTypeId = EnsureLookupItem(conn, tx, "video_type", row.VideoTypeName);

                // Используем фактические значения после приведения типов, а не "строка не пустая".
                // Иначе можно вставить weapon_parts с NULL во всех полях и потом строка "пропадет" после LoadWeaponsWithMeta().
                string? serialNumber = string.IsNullOrWhiteSpace(row.SerialNumber) ? null : row.SerialNumber;
                decimal? frequencyMhz = ParseDecimalMhz(row.FrequencyMhz);

                var hasPartsMetaActual =
                    serialNumber != null ||
                    videoTypeId.HasValue ||
                    frequencyMhz.HasValue;

                if (hasTypeMeta)
                {
                    using var updWeapon = conn.CreateCommand();
                    updWeapon.Transaction = tx;
                    updWeapon.CommandText = "update weapon set type_id=@tid where id=@wid";
                    var pTidUpd = updWeapon.CreateParameter();
                    pTidUpd.ParameterName = "@tid";
                    pTidUpd.Value = weaponTypeId.HasValue ? weaponTypeId.Value : (object)System.DBNull.Value;
                    var pWidUpd = updWeapon.CreateParameter();
                    pWidUpd.ParameterName = "@wid";
                    pWidUpd.Value = weaponId;
                    updWeapon.Parameters.Add(pTidUpd);
                    updWeapon.Parameters.Add(pWidUpd);
                    updWeapon.ExecuteNonQuery();
                }

                if (hasPartsMetaActual)
                {
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = @"
insert into weapon_parts(weapon_id, serial_number, video_type_id, frequency_mhz)
values(@wid, @sn, @vid, @fm)";
                    var pWid = ins.CreateParameter();
                    pWid.ParameterName = "@wid";
                    pWid.Value = weaponId;
                    var pSn = ins.CreateParameter();
                    pSn.ParameterName = "@sn";
                    pSn.Value = serialNumber == null ? (object)System.DBNull.Value : serialNumber;
                    var pVid = ins.CreateParameter();
                    pVid.ParameterName = "@vid";
                    pVid.Value = videoTypeId.HasValue ? videoTypeId.Value : (object)System.DBNull.Value;
                    var pFm = ins.CreateParameter();
                    pFm.ParameterName = "@fm";
                    pFm.Value = frequencyMhz.HasValue ? (object)frequencyMhz.Value : System.DBNull.Value;
                    ins.Parameters.Add(pWid);
                    ins.Parameters.Add(pSn);
                    ins.Parameters.Add(pVid);
                    ins.Parameters.Add(pFm);
                    ins.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static void EnsureWeaponMetaTables(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
if object_id('dbo.weapon_type','U') is null
begin
    create table dbo.weapon_type(
        id int identity(1,1) not null primary key,
        name nvarchar(255) null
    );
end

if object_id('dbo.subtype','U') is null
begin
    create table dbo.subtype(
        id int identity(1,1) not null primary key,
        name nvarchar(255) null
    );
end

if col_length('dbo.weapon','subtype_id') is null
begin
    alter table dbo.weapon add subtype_id int null;
end

if object_id('dbo.video_type','U') is null
begin
    create table dbo.video_type(
        id int identity(1,1) not null primary key,
        name nvarchar(255) null
    );
end

if object_id('dbo.weapon_parts','U') is null
begin
    create table dbo.weapon_parts(
        id int identity(1,1) not null primary key,
        weapon_id int not null,
        serial_number nvarchar(255) null,
        video_type_id int null,
        frequency_mhz decimal(10,3) null
    );
end

if object_id('dbo.weapon_parts','U') is not null and col_length('dbo.weapon_parts','id') is null
begin
    alter table dbo.weapon_parts add id int identity(1,1) not null;
    if not exists (
        select 1
        from sys.indexes
        where name = N'PK_weapon_parts_id'
    )
    begin
        alter table dbo.weapon_parts add constraint PK_weapon_parts_id primary key(id);
    end
end

if object_id('dbo.weapon_parts','U') is not null and col_length('dbo.weapon_parts','frequency_mhz') is null
begin
    alter table dbo.weapon_parts add frequency_mhz decimal(10,3) null;
end";
        cmd.ExecuteNonQuery();
    }

    private static decimal? ParseDecimalMhz(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Нормализуем ввод частоты: поддерживаем и ',' и '.' как десятичный разделитель.
        // Также удаляем пробелы (возможны как разделители тысяч).
        var s = input.Trim().Replace(" ", string.Empty);

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');

        // Определяем десятичный разделитель по последнему вхождению '.' или ','.
        var decimalSep = lastComma >= 0 && lastDot >= 0
            ? (lastComma > lastDot ? ',' : '.')
            : (lastComma >= 0 ? ',' : '.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (decimalSep == ',')
            {
                // '.' считаем разделителем тысяч.
                s = s.Replace(".", string.Empty);
                s = s.Replace(',', '.');
            }
            else
            {
                // ',' считаем разделителем тысяч.
                s = s.Replace(",", string.Empty);
            }
        }
        else if (lastComma >= 0)
        {
            // Только ',' - считаем его десятичным.
            s = s.Replace(',', '.');
        }
        else
        {
            // Только '.' или без разделителей: оставляем '.' как десятичный (если он есть).
            // Ничего не делаем, кроме удаления тысячевых ',' (если они вдруг есть, но это уже не так).
            // Если у пользователя культура с тысячным ',', но '.' является единственным разделителем,
            // то запятые мы просто удалим как тысячные.
            if (s.IndexOf(',', System.StringComparison.Ordinal) >= 0)
                s = s.Replace(",", string.Empty);
        }

        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;

        return null;
    }

    private static int EnsureLookupItem(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx, string table, string name)
    {
        using (var find = conn.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = $"select top(1) id from {table} where name = @n";
            var p = find.CreateParameter();
            p.ParameterName = "@n";
            p.Value = name;
            find.Parameters.Add(p);
            var existing = find.ExecuteScalar();
            if (existing != null && existing != System.DBNull.Value)
                return System.Convert.ToInt32(existing);
        }

        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = $"insert into {table}(name) values (@n); select cast(scope_identity() as int);";
            var p = ins.CreateParameter();
            p.ParameterName = "@n";
            p.Value = name;
            ins.Parameters.Add(p);
            return System.Convert.ToInt32(ins.ExecuteScalar());
        }
    }

    private void LoadWeaponTypeOptionsFromDb(Data.AppDbContext db)
    {
        WeaponTypeOptions.Clear();
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            EnsureWeaponMetaTables(conn, tx: null);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select name from weapon_type where name is not null order by name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var name = rd.IsDBNull(0) ? string.Empty : rd.GetString(0);
                if (!string.IsNullOrWhiteSpace(name))
                    WeaponTypeOptions.Add(name);
            }
        }
        catch
        {
            // Не блокуємо UI, якщо довідник типів ще не налаштований.
        }
    }

    private void LoadWeaponNameOptionsFromDb(Data.AppDbContext db)
    {
        WeaponNameOptions.Clear();
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
select
    w.id,
    coalesce(
        nullif(ltrim(rtrim(w.name)), ''),
        nullif(ltrim(rtrim(w.code)), '')
    ) as weapon_name
from weapon w
where
    (w.name is not null and ltrim(rtrim(w.name)) <> '')
    or
    (w.code is not null and ltrim(rtrim(w.code)) <> '')
order by weapon_name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var id = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                var weaponName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
                if (id <= 0 || string.IsNullOrWhiteSpace(weaponName))
                    continue;

                WeaponNameOptions.Add(new WeaponOption
                {
                    Id = id,
                    WeaponName = weaponName,
                    DisplayName = weaponName
                });
            }
        }
        catch
        {
            // ignore
        }
    }

    private void LoadVideoTypeOptionsFromDb(Data.AppDbContext db)
    {
        VideoTypeOptions.Clear();
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                conn.Open();

            EnsureWeaponMetaTables(conn, tx: null);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select name from video_type where name is not null order by name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var name = rd.IsDBNull(0) ? string.Empty : rd.GetString(0);
                if (!string.IsNullOrWhiteSpace(name))
                    VideoTypeOptions.Add(name);
            }
        }
        catch
        {
            // Не блокуємо UI, якщо довідник типів еще не налаштований.
        }
    }

    private static Dictionary<int, string> LoadPilotSquads(Data.AppDbContext db)
    {
        var result = new Dictionary<int, string>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        if (!TableExists(conn, "squad"))
            return result;

        var mapTable = DetectPilotSquadMapTable(conn);
        if (mapTable == null)
            return result;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
select sp.pilot_id, sq.name
from squad_rel sp
left join squad sq on sq.id = sp.squad_id";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            if (rd.IsDBNull(0))
                continue;
            var pilotId = rd.GetInt32(0);
            var squadName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1);
            if (!result.ContainsKey(pilotId))
                result[pilotId] = squadName;
        }

        return result;
    }

    private static void SavePilotSquads(Data.AppDbContext db, List<Row> rows)
    {
        // EF-запросы выполняем до локальной ADO-транзакции.
        var pilotsByName = db.Pilots
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToList()
            .GroupBy(p => (p.Name ?? string.Empty).Trim(), System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, System.StringComparer.OrdinalIgnoreCase);

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var tx = conn.BeginTransaction();
        try
        {
            EnsureSquadTable(conn, tx);
            var mapTable = EnsurePilotSquadMapTable(conn, tx);

            // Полный пересчет связей для текущего списка пилотов.
            using (var delCmd = conn.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = $"delete from squad_rel";
                delCmd.ExecuteNonQuery();
            }

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.SquadName))
                    continue;

                int pilotId = row.Id;
                if (pilotId <= 0)
                {
                    if (!pilotsByName.TryGetValue(row.Name, out pilotId))
                        continue;
                }

                var squadId = EnsureSquad(conn, tx, row.SquadName);
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = $"insert into squad_rel(pilot_id, squad_id) values (@pid, @sid)";
                var p1 = ins.CreateParameter();
                p1.ParameterName = "@pid";
                p1.Value = pilotId;
                var p2 = ins.CreateParameter();
                p2.ParameterName = "@sid";
                p2.Value = squadId;
                ins.Parameters.Add(p1);
                ins.Parameters.Add(p2);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string EnsurePilotSquadMapTable(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx)
    {
        var existing = DetectPilotSquadMapTable(conn, tx);
        if (existing != null)
            return existing;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
if object_id('dbo.squad_pilot','U') is null
begin
    create table dbo.squad_pilot(
        pilot_id int not null,
        squad_id int not null
    );
end";
        cmd.ExecuteNonQuery();
        return "squad_pilot";
    }

    private static void EnsureSquadTable(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx)
    {
        if (TableExists(conn, "squad", tx))
            return;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
create table dbo.squad(
    id int identity(1,1) not null primary key,
    name nvarchar(255) null
);";
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(System.Data.Common.DbConnection conn, string tableName,
        System.Data.Common.DbTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $@"
if exists (select 1 from information_schema.tables where table_name = '{tableName}')
    select 1
else
    select 0";
        return System.Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static string? DetectPilotSquadMapTable(System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction? tx = null)
    {
        var candidates = new[] { "squad_rel", "squad_pilot", "pilot_squad", "squad_map", "squad" };
        foreach (var table in candidates)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
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
            var exists = System.Convert.ToInt32(checkCmd.ExecuteScalar());
            if (exists == 1)
                return table;
        }
        return null;
    }

    private static int EnsureSquad(System.Data.Common.DbConnection conn, System.Data.Common.DbTransaction tx, string squadName)
    {
        using (var find = conn.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = "select top(1) id from squad where name = @n";
            var p = find.CreateParameter();
            p.ParameterName = "@n";
            p.Value = squadName;
            find.Parameters.Add(p);
            var existing = find.ExecuteScalar();
            if (existing != null && existing != System.DBNull.Value)
                return System.Convert.ToInt32(existing);
        }

        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = "insert into squad(name) values (@n); select cast(scope_identity() as int);";
            var p = ins.CreateParameter();
            p.ParameterName = "@n";
            p.Value = squadName;
            ins.Parameters.Add(p);
            return System.Convert.ToInt32(ins.ExecuteScalar());
        }
    }
}
