using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace AirLiticApp;

public partial class SimpleDictionaryWindow : Window
{
    private readonly SimpleDictionaryKind _kind;
    private readonly ObservableCollection<Row> _rows = new();

    public SimpleDictionaryWindow(SimpleDictionaryKind kind)
    {
        _kind = kind;
        InitializeComponent();
        ItemsGrid.ItemsSource = _rows;
        LoadData();
    }

    private void LoadData()
    {
        _rows.Clear();
        using var db = new Data.AppDbContext();

        switch (_kind)
        {
            case SimpleDictionaryKind.Pilots:
                Title = "Пілоти";
                foreach (var x in db.Pilots.OrderBy(p => p.Name))
                    _rows.Add(new Row { Id = x.Id, Name = x.Name });
                break;
            case SimpleDictionaryKind.Weapons:
                Title = "Засоби";
                foreach (var x in db.Weapons.OrderBy(w => w.Name))
                    _rows.Add(new Row { Id = x.Id, Name = x.Name });
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

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        _rows.Add(new Row { Id = 0, Name = "" });
        ItemsGrid.SelectedIndex = _rows.Count - 1;
        ItemsGrid.ScrollIntoView(ItemsGrid.SelectedItem);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Row row)
            return;

        _rows.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cleaned = _rows
            .Select(r => new Row { Id = r.Id, Name = (r.Name ?? "").Trim() })
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToList();

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
    }

    private static void SaveWeapons(Data.AppDbContext db, System.Collections.Generic.List<Row> rows)
    {
        var existing = db.Weapons.ToDictionary(x => x.Id);
        ApplySimpleEdits(
            rows,
            existing.Keys.ToHashSet(),
            id => existing[id].Name,
            (id, name) => existing[id].Name = name,
            name => db.Weapons.Add(new Models.Weapon { Name = name }),
            id => db.Weapons.Remove(existing[id]));
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
    }
}
