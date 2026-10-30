using SQLite;

namespace RetroShite.Models;

public sealed class CollectionSystem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique, NotNull]
    public string Name { get; set; } = "";
}
