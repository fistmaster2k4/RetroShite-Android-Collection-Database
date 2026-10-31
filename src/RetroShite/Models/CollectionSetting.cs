using SQLite;

namespace RetroShite.Models;

public sealed class CollectionSetting
{
    [PrimaryKey]
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
