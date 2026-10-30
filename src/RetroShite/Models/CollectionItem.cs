using SQLite;

namespace RetroShite.Models;

public sealed class CollectionItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Barcode { get; set; } = "";
    public string System { get; set; } = "";
    public string Title { get; set; } = "";
    public string Format { get; set; } = "Boxed";
    public string CoverFile { get; set; } = "";
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}
