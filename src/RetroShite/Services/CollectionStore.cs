using RetroShite.Models;
using SQLite;

namespace RetroShite.Services;

public sealed class CollectionStore
{
    public static readonly string Root = Path.Combine(FileSystem.AppDataDirectory, "Collection");
    public static readonly string Covers = Path.Combine(Root, "Covers");
    public static readonly string Database = Path.Combine(Root, "retroshite.db");
    private readonly SQLiteConnection _db;

    private static readonly string[] DefaultSystems =
    {
        "Mega Drive", "Mega-CD", "Master System", "Saturn", "Dreamcast",
        "NES", "SNES", "Nintendo 64", "GameCube", "Game Boy", "Game Boy Advance",
        "PlayStation", "PS2", "PS3", "PSP Games", "PSP UMD Video", "DVD", "Blu-ray", "Other"
    };

    public CollectionStore()
    {
        Directory.CreateDirectory(Covers);
        _db = new SQLiteConnection(Database);
        _db.CreateTable<CollectionItem>();
        _db.CreateTable<CollectionSystem>();
        _db.CreateTable<CollectionSetting>();
        // Seed only on first migration. An intentionally emptied category list must stay empty.
        if (_db.Find<CollectionSetting>("systems_initialized") is null)
        {
            if (_db.Table<CollectionSystem>().Count() == 0)
            {
                foreach (var name in DefaultSystems)
                    _db.Insert(new CollectionSystem { Name = name });
            }
            _db.Insert(new CollectionSetting { Key = "systems_initialized", Value = "1" });
        }
    }

    public List<string> Systems()
    {
        var configured = _db.Table<CollectionSystem>().ToList().OrderBy(x => x.Id).Select(x => x.Name);
        var used = _db.Table<CollectionItem>().ToList().Select(x => x.System);
        return configured.Concat(used).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool AddSystem(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Equals("All", StringComparison.OrdinalIgnoreCase)
            || Systems().Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;
        _db.Insert(new CollectionSystem { Name = name });
        return true;
    }

    public int ItemCountForSystem(string name) =>
        _db.Table<CollectionItem>().Count(x => x.System == name);

    public void RemoveSystems(IEnumerable<string> names)
    {
        var toRemove = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (toRemove.Count == 0) return;
        var categories = _db.Table<CollectionSystem>().ToList();
        if (categories.Count - toRemove.Count < 1)
            throw new InvalidOperationException("Keep at least one system. You can rename your collection by adding a new one first.");
        if (toRemove.Any(name => ItemCountForSystem(name) != 0))
            throw new InvalidOperationException("Move or delete the items in a system before removing it.");
        _db.RunInTransaction(() =>
        {
            foreach (var category in categories.Where(x => toRemove.Contains(x.Name, StringComparer.OrdinalIgnoreCase)))
                _db.Delete(category);
        });
    }

    public List<CollectionItem> All() => _db.Table<CollectionItem>().OrderBy(x => x.Title).ToList();
    public CollectionItem? Lookup(string barcode) => _db.Table<CollectionItem>().FirstOrDefault(x => x.Barcode == barcode);
    public void Save(CollectionItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Barcode))
        {
            var existing = Lookup(item.Barcode);
            if (existing is not null && existing.Id != item.Id)
                throw new InvalidOperationException("This barcode is already in the collection.");
        }
        if (item.Id == 0) _db.Insert(item);
        else _db.Update(item);
    }
    public void Delete(CollectionItem item)
    {
        _db.Delete(item);
        if (!string.IsNullOrWhiteSpace(item.CoverFile))
        {
            var path = Path.Combine(Covers, item.CoverFile);
            if (File.Exists(path) && !_db.Table<CollectionItem>().Any(x => x.CoverFile == item.CoverFile)) File.Delete(path);
        }
    }
    public void Close() => _db.Close();
}
