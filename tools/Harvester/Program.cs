using System.Text.Json;
using Harvester;
using WrestlingGameMuseum.Core;

// Usage: dotnet run --project tools/Harvester -- <facts|art|build|sheet> [--refresh]
// Paths are relative to the repository root (found by walking up to the .sln).
string root = FindRoot();
string cache = Path.Combine(root, "tools", "cache");
string outDir = Path.Combine(root, "tools", "out");
Directory.CreateDirectory(outDir);
string command = args.Length > 0 ? args[0] : "build";
bool refresh = args.Contains("--refresh");

CurationFile curation = CurationFile.Load(Path.Combine(root, "data", "curation.json"));
using var http = new PoliteHttp(cache);

var factsBySlug = new Dictionary<string, ArchiveFacts?>();
var artBySlug = new Dictionary<string, (LaunchBoxResult? Entry, BoxFront? Front, string Note)>();

foreach (CurationEntry c in curation.Games)
{
    Console.WriteLine($"[{c.Slug}]");
    factsBySlug[c.Slug] = await FetchFactsAsync(c);
    if (command is "art" or "build" or "sheet")
        artBySlug[c.Slug] = await FindArtAsync(c, factsBySlug[c.Slug]);
}

if (command is "build" or "art")
{
    var games = curation.Games
        .Select(c => CatalogBuilder.Build(c, factsBySlug[c.Slug], artBySlug[c.Slug].Entry, artBySlug[c.Slug].Front))
        .OrderBy(g => g.FirstDate, StringComparer.Ordinal).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToList();
    var catalog = new Catalog { Version = 1, Generated = DateTime.UtcNow.ToString("yyyy-MM-dd"), Games = games };
    if (command == "build")
    {
        File.WriteAllText(Path.Combine(root, "data", "games.json"), CatalogLoader.Serialize(catalog) + "\n");
        Console.WriteLine($"Wrote data/games.json: {games.Count} games, {games.Count(g => g.HasBoxArt)} with box art.");
    }
    var report = artBySlug.Select(kv => new { slug = kv.Key, picked = kv.Value.Front?.Url, region = kv.Value.Front?.Region, note = kv.Value.Note });
    File.WriteAllText(Path.Combine(outDir, "art-report.json"), JsonSerializer.Serialize(report, CatalogLoader.Options));
}

if (command is "sheet" or "build")
    WriteContactSheet(artBySlug, factsBySlug, Path.Combine(outDir, "contact-sheet.html"));

return 0;

async Task<ArchiveFacts?> FetchFactsAsync(CurationEntry c)
{
    if (c.Slug.Length == 0) return null;
    try
    {
        string html = await http.GetStringAsync("https://www.thesmackdownhotel.com/games/" + c.Slug, refresh);
        ArchiveFacts? facts = SmackdownParser.Parse(c.Slug, html);
        if (facts is null) Console.Error.WriteLine("  !! archive page has no fields");
        return facts;
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine("  !! archive page unavailable: " + ex.Message);
        return null;
    }
}

async Task<(LaunchBoxResult?, BoxFront?, string)> FindArtAsync(CurationEntry c, ArchiveFacts? facts)
{
    string title = c.Title ?? facts?.Title ?? c.Slug;
    LaunchBoxResult? entry = null;
    if (c.LaunchBoxId.Length > 0)
        entry = new LaunchBoxResult(c.LaunchBoxId, title, "");
    else
    {
        string query = c.LaunchBoxQuery.Length > 0 ? c.LaunchBoxQuery : title;
        string html;
        try { html = await http.GetStringAsync(LaunchBoxParser.SearchUrl(query), refresh); }
        catch (HttpRequestException ex) { return (null, null, "LaunchBox search failed: " + ex.Message); }
        var results = LaunchBoxParser.ParseSearch(html);
        string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        entry = results.FirstOrDefault(r => Norm(r.Title) == Norm(query));
        if (entry is null)
            return (null, null, "no exact title match; candidates: " + string.Join(" | ", results.Take(5).Select(r => $"{r.Title} [{r.Platform}] {r.IdSlug}")));
    }
    if (c.NoBoxArtReason.Length > 0) return (entry, null, "skipped: " + c.NoBoxArtReason);
    string imagesHtml = await http.GetStringAsync(entry.ImagesUrl, refresh);
    var fronts = LaunchBoxParser.ParseFronts(imagesHtml);
    BoxFront? pick = LaunchBoxParser.PickFront(fronts, c.JapanOnly);
    if (pick is null)
        return (entry, null, $"{fronts.Count} box fronts on {entry.IdSlug}, none usable (logo/banner only)");
    Console.WriteLine($"  art: {pick.Region} {pick.Width}x{pick.Height} ({entry.IdSlug})");
    return (entry, pick, $"{entry.IdSlug}: {pick.Title}");
}

void WriteContactSheet(Dictionary<string, (LaunchBoxResult? Entry, BoxFront? Front, string Note)> art,
    Dictionary<string, ArchiveFacts?> facts, string path)
{
    // Open this file in a browser (images load from LaunchBox) and review every box by eye before committing a batch.
    var sb = new System.Text.StringBuilder("<!doctype html><meta charset=utf-8><body style='background:#111;color:#ddd;font:12px sans-serif'>"
        + "<div style='display:grid;grid-template-columns:repeat(10,1fr);gap:8px'>");
    foreach (var (slug, v) in art)
    {
        sb.Append("<figure style='margin:0;text-align:center'>");
        sb.Append(v.Front is null
            ? "<div style='height:180px;border:1px dashed #a55;display:flex;align-items:center;justify-content:center'>no box</div>"
            : $"<img src='{v.Front.Url}' style='height:180px;max-width:100%;object-fit:contain'>");
        sb.Append($"<figcaption>{System.Net.WebUtility.HtmlEncode(facts[slug]?.Title ?? slug)}<br>{v.Front?.Region}</figcaption></figure>");
    }
    File.WriteAllText(path, sb + "</div>");
}

static string FindRoot()
{
    string? dir = AppContext.BaseDirectory;
    while (dir is not null && !Directory.GetFiles(dir, "*.sln").Any()) dir = Path.GetDirectoryName(dir);
    return dir ?? Directory.GetCurrentDirectory();
}
