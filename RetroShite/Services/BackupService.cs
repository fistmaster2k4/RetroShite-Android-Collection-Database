using System.IO.Compression;
using SQLite;

namespace RetroShite.Services;

public static class BackupService
{
    public static async Task<string> ExportAsync(CollectionStore store)
    {
        // Close SQLite before copying: avoid copying a live database with unflushed changes.
        store.Close();
        var path = Path.Combine(FileSystem.CacheDirectory, $"RetroShite_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(CollectionStore.Database, "retroshite.db", CompressionLevel.Optimal);
            foreach (var cover in Directory.EnumerateFiles(CollectionStore.Covers))
                zip.CreateEntryFromFile(cover, "covers/" + Path.GetFileName(cover), CompressionLevel.Optimal);
        }
#if ANDROID
        var resolver = Android.App.Application.Context.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver unavailable.");
        using var values = new Android.Content.ContentValues();
        values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, Path.GetFileName(path));
        values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "application/zip");
        values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath,
            Android.OS.Environment.DirectoryDownloads + "/RetroShite");
        var uri = resolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new IOException("Unable to create Downloads file.");
        await using var output = resolver.OpenOutputStream(uri)
            ?? throw new IOException("Unable to open Downloads file.");
        await using var input = File.OpenRead(path);
        await input.CopyToAsync(output);
        return "Downloads/RetroShite/" + Path.GetFileName(path);
#else
        return path;
#endif
    }

    public static async Task ImportAsync(FileResult selected, CollectionStore store)
    {
        var staging = Path.Combine(FileSystem.CacheDirectory, "retroshite_import_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await using (var source = await selected.OpenReadAsync())
            await using (var copy = File.Create(Path.Combine(staging, "input.zip")))
                await source.CopyToAsync(copy);
            var dbPath = Path.Combine(staging, "retroshite.db");
            var coverDir = Path.Combine(staging, "covers");
            Directory.CreateDirectory(coverDir);
            using (var zip = ZipFile.OpenRead(Path.Combine(staging, "input.zip")))
            {
                foreach (var entry in zip.Entries)
                {
                    // Only allow expected filenames, never extract arbitrary paths from an archive.
                    string? target = entry.FullName switch
                    {
                        "retroshite.db" => dbPath,
                        _ when entry.FullName.StartsWith("covers/", StringComparison.Ordinal)
                            && Path.GetFileName(entry.FullName) == entry.FullName[7..]
                            && Path.GetExtension(entry.FullName).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                            => Path.Combine(coverDir, Path.GetFileName(entry.FullName)),
                        _ => null
                    };
                    if (target is null || entry.Length > 40_000_000) continue;
                    entry.ExtractToFile(target, true);
                }
            }
            if (!File.Exists(dbPath)) throw new InvalidDataException("Archive has no RetroShite database.");
            using (var probe = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
            {
                if (!probe.GetTableInfo("CollectionItem").Any())
                    throw new InvalidDataException("Not a valid RetroShite database.");
            }
            store.Close();
            // Preserve the existing collection on disk until the new one has been validated.
            var rollback = Path.Combine(staging, "old.db");
            if (File.Exists(CollectionStore.Database)) File.Copy(CollectionStore.Database, rollback);
            try
            {
                File.Copy(dbPath, CollectionStore.Database, true);
                foreach (var cover in Directory.EnumerateFiles(coverDir))
                    File.Copy(cover, Path.Combine(CollectionStore.Covers, Path.GetFileName(cover)), true);
            }
            catch
            {
                if (File.Exists(rollback)) File.Copy(rollback, CollectionStore.Database, true);
                throw;
            }
        }
        finally { Directory.Delete(staging, true); }
    }
}
