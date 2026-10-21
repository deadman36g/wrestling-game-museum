using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrestlingGameMuseum.Core;

public static class CatalogLoader
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static Catalog Parse(string json) =>
        JsonSerializer.Deserialize<Catalog>(json, Options) ?? new Catalog();

    public static Catalog Load(string path) => Parse(File.ReadAllText(path));

    public static string Serialize(Catalog catalog) => JsonSerializer.Serialize(catalog, Options);
}
