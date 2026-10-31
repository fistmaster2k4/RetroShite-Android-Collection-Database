using System.IO.Compression;
using RetroShite.Models;
using SQLite;

namespace RetroShite.Services;

public readonly record struct WorkProgress(string Stage, int Done, int Total);
public readonly record struct ImportResult(int Added, int Skipped, int SystemsAdded);

public static class BackupService
{
    private static void Update(IProgress<WorkProgress>? progress, string stage, int done, int total) =>
        progress?.Report(new WorkProgress(stage, done, total));

    public static async Task<string> ExportAsync(CollectionStore store, IProgress<WorkProgress>? progress = null)
    {
        store.Close();
        var path = Path.Combine(FileSystem.CacheDirectory, $"RetroShite_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        try
        {
            var covers = Directory.GetFiles(CollectionStore.Covers, "*.jpg");
            await Task.Run(() =>
            {
                Update(progress, "Preparing database", 0, covers.Length);
                using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(CollectionStore.Database, "retroshite.db", CompressionLevel.Optimal);
                for (var i = 0; i < covers.Length; i++)
                {
                    zip.CreateEntryFromFile(covers[i], "covers/" + Path.GetFileName(covers[i]), CompressionLevel.Optimal);
                    Update(progress, "Adding covers", i + 1, covers.Length);
                }
            });
#if RETROSHITE_LEGACY
            Update(progress, "Choose where to save ZIP", covers.Length, covers.Length);
#else
            Update(progress, "Saving ZIP to Downloads", covers.Length, covers.Length);
#endif
#if ANDROID
            var resolver = Android.App.Application.Context.ContentResolver
                ?? throw new InvalidOperationException("Android content resolver unavailable.");
#if RETROSHITE_LEGACY
            // SAF is available on Android 8 and does not require storage permission.
            // Let the user choose a destination; modern builds retain Downloads/RetroShite.
            var uri = await MainThread.InvokeOnMainThreadAsync(() =>
                MainActivity.ChooseExportDestinationAsync(Path.GetFileName(path)));
            if (uri is null) throw new OperationCanceledException("Export cancelled.");
            await using (var output = resolver.OpenOutputStream(uri)
                ?? throw new IOException("Unable to open selected export destination."))
            await using (var input = File.OpenRead(path))
                await input.CopyToAsync(output);
            return "Saved to selected document location";
#else
            using var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, Path.GetFileName(path));
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "application/zip");
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath,
                Android.OS.Environment.DirectoryDownloads + "/RetroShite");
            var uri = resolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values)
                ?? throw new IOException("Unable to create Downloads file.");
            try
            {
                await using var output = resolver.OpenOutputStream(uri)
                    ?? throw new IOException("Unable to open Downloads file.");
                await using var input = File.OpenRead(path);
                await input.CopyToAsync(output);
            }
            catch
            {
                resolver.Delete(uri, null, null);
                throw;
            }
            return "Downloads/RetroShite/" + Path.GetFileName(path);
#endif
#else
            return path;
#endif
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static async Task<ImportResult> ImportAsync(FileResult selected, CollectionStore store,
        bool merge, IProgress<WorkProgress>? progress = null)
    {
        var staging = Path.Combine(FileSystem.CacheDirectory, "retroshite_import_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            Update(progress, "Reading backup", 0, 1);
            await using (var source = await selected.OpenReadAsync())
            await using (var copy = File.Create(Path.Combine(staging, "input.zip")))
                await source.CopyToAsync(copy);
            var dbPath = Path.Combine(staging, "retroshite.db");
            var coverDir = Path.Combine(staging, "covers");
            Directory.CreateDirectory(coverDir);
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(Path.Combine(staging, "input.zip"));
                var entries = zip.Entries.Where(e => e.FullName == "retroshite.db" ||
                    (e.FullName.StartsWith("covers/", StringComparison.Ordinal) &&
                     Path.GetFileName(e.FullName) == e.FullName[7..] &&
                     Path.GetExtension(e.FullName).Equals(".jpg", StringComparison.OrdinalIgnoreCase))).ToList();
                if (entries.Count > 100_000 || entries.Sum(e => e.Length) > 10_000_000_000L)
                    throw new InvalidDataException("Backup exceeds import limits.");
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Length > 40_000_000) throw new InvalidDataException("An archive entry is too large.");
                    entry.ExtractToFile(entry.FullName == "retroshite.db" ? dbPath :
                        Path.Combine(coverDir, Path.GetFileName(entry.FullName)), true);
                    Update(progress, "Extracting backup", i + 1, entries.Count);
                }
            });
            if (!File.Exists(dbPath)) throw new InvalidDataException("Archive has no RetroShite database.");
            using (var probe = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
                if (!probe.GetTableInfo("CollectionItem").Any() || !probe.GetTableInfo("CollectionSystem").Any())
                    throw new InvalidDataException("Not a valid RetroShite database.");
            store.Close();
            return await Task.Run(() => merge ? Merge(dbPath, coverDir, progress) :
                Replace(dbPath, coverDir, staging, progress));
        }
        finally { Directory.Delete(staging, true); }
    }

    private static ImportResult Merge(string dbPath, string coverDir, IProgress<WorkProgress>? progress)
    {
        using var incoming = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
        using var current = new SQLiteConnection(CollectionStore.Database);
        var items = incoming.Table<CollectionItem>().ToList();
        var systems = incoming.Table<CollectionSystem>().ToList();
        var existing = current.Table<CollectionItem>().ToList();
        var seenBarcodes = new HashSet<string>(existing.Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
            .Select(i => i.Barcode.Trim()), StringComparer.OrdinalIgnoreCase);
        var seenManualTitles = new HashSet<string>(existing.Where(i => string.IsNullOrWhiteSpace(i.Barcode))
            .Select(Key), StringComparer.OrdinalIgnoreCase);
        var knownSystems = new HashSet<string>(current.Table<CollectionSystem>().ToList().Select(s => s.Name),
            StringComparer.OrdinalIgnoreCase);
        var copiedCovers = new List<string>();
        var added = 0;
        var skipped = 0;
        var systemsAdded = 0;
        try
        {
            current.RunInTransaction(() =>
            {
                foreach (var name in systems.Select(s => s.Name).Concat(items.Select(i => i.System)))
                {
                    if (string.IsNullOrWhiteSpace(name) || !knownSystems.Add(name.Trim())) continue;
                    current.Insert(new CollectionSystem { Name = name.Trim() });
                    systemsAdded++;
                }
                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    var barcode = item.Barcode?.Trim() ?? "";
                    var key = Key(item);
                    // Distinct barcodes can represent distinct editions of the same title.
                    // Only barcode-less records are matched by system/title/condition.
                    if (barcode.Length > 0 ? seenBarcodes.Contains(barcode) : seenManualTitles.Contains(key))
                        skipped++;
                    else
                    {
                        item.Id = 0;
                        item.Barcode = barcode;
                        if (!string.IsNullOrWhiteSpace(item.CoverFile))
                        {
                            var source = Path.Combine(coverDir, Path.GetFileName(item.CoverFile));
                            if (File.Exists(source))
                            {
                                var targetName = Guid.NewGuid().ToString("N") + ".jpg";
                                var target = Path.Combine(CollectionStore.Covers, targetName);
                                File.Copy(source, target);
                                copiedCovers.Add(target);
                                item.CoverFile = targetName;
                            }
                            else item.CoverFile = "";
                        }
                        current.Insert(item);
                        if (barcode.Length > 0) seenBarcodes.Add(barcode);
                        if (barcode.Length == 0) seenManualTitles.Add(key);
                        added++;
                    }
                    Update(progress, "Merging titles", index + 1, items.Count);
                }
            });
        }
        catch
        {
            foreach (var cover in copiedCovers)
                if (File.Exists(cover)) File.Delete(cover);
            throw;
        }
        return new ImportResult(added, skipped, systemsAdded);
    }

    private static string Key(CollectionItem item) =>
        (item.System ?? "").Trim().ToUpperInvariant() + "\u001f" +
        (item.Title ?? "").Trim().ToUpperInvariant() + "\u001f" +
        (item.Format ?? "").Trim().ToUpperInvariant();

    private static ImportResult Replace(string dbPath, string coverDir, string staging,
        IProgress<WorkProgress>? progress)
    {
        var rollback = Path.Combine(staging, "old.db");
        if (File.Exists(CollectionStore.Database)) File.Copy(CollectionStore.Database, rollback);
        try
        {
            Update(progress, "Replacing collection", 0, 1);
            File.Copy(dbPath, CollectionStore.Database, true);
            foreach (var cover in Directory.EnumerateFiles(coverDir))
                File.Copy(cover, Path.Combine(CollectionStore.Covers, Path.GetFileName(cover)), true);
            Update(progress, "Replacing collection", 1, 1);
        }
        catch
        {
            if (File.Exists(rollback)) File.Copy(rollback, CollectionStore.Database, true);
            throw;
        }
        return new ImportResult(0, 0, 0);
    }
}
