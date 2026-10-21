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

using var http = new PoliteHttp(cache);

if (command == "list")
{
    await ListAsync();
    return 0;
}

CurationFile curation = CurationFile.Load(Path.Combine(root, "data", "curation.json"));

var factsBySlug = new Dictionary<string, ArchiveFacts?>();
var artBySlug = new Dictionary<string, (LaunchBoxResult? Entry, BoxFront? Front, string Note)>();

var entries = curation.Games.ToList();
string slugFile = Path.Combine(root, "data", "slugs.txt");
if (File.Exists(slugFile))
    foreach (string slug in File.ReadAllLines(slugFile).Select(l => l.Trim()).Where(l => l.Length > 0))
        if (entries.All(e => e.Slug != slug)) entries.Add(new CurationEntry { Slug = slug });
entries.RemoveAll(e => e.Exclude);
if (args.Contains("--only")) { var only = args[(Array.IndexOf(args, "--only") + 1)..].Where(a => !a.StartsWith("--")).ToHashSet(); entries = entries.Where(e => only.Contains(e.Slug)).ToList(); }

foreach (CurationEntry c in entries)
{
    Console.WriteLine($"[{c.Slug}]");
    factsBySlug[c.Slug] = await FetchFactsAsync(c);
    if (command is "art" or "build" or "sheet")
        artBySlug[c.Slug] = await FindArtAsync(c, factsBySlug[c.Slug]);
}

if (command is "build" or "art")
{
    var games = entries
        .Where(c => factsBySlug[c.Slug] is not null || c.Title is not null)
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
    var candidates = new List<LaunchBoxResult>();
    string note = "";
    if (c.LaunchBoxId.Length > 0)
        candidates.Add(new LaunchBoxResult(c.LaunchBoxId, title, ""));
    else
    {
        string query = c.LaunchBoxQuery.Length > 0 ? c.LaunchBoxQuery : title;
        string html;
        try { html = await http.GetStringAsync(LaunchBoxParser.SearchUrl(query), refresh); }
        catch (HttpRequestException ex) { return (null, null, "LaunchBox search failed: " + ex.Message); }
        var results = LaunchBoxParser.ParseSearch(html);
        var platforms = c.Platforms.Count > 0 ? c.Platforms : facts?.Platforms ?? [];
        // Same-titled entries exist for several platforms: only those on one of the archive's platforms count.
        // Best first: exact title, then close title; within that, the archive's first-listed platform.
        var scored = results.Select(r => (r, rank: PlatformMap.Rank(platforms, r.Platform), match: TitleMatch.Score(r.Title, title, query)))
                            .Where(x => x.rank >= 0 && x.match > 0).ToList();
        candidates = scored.OrderByDescending(x => x.match).ThenBy(x => x.rank).Select(x => x.r).ToList();
        if (candidates.Count == 0)
            return (null, null, "no title+platform match; candidates: " + string.Join(" | ", results.Take(5).Select(r => $"{r.Title} [{r.Platform}] {r.IdSlug}")));
    }
    if (c.NoBoxArtReason.Length > 0) return (candidates[0], null, "skipped: " + c.NoBoxArtReason);
    foreach (LaunchBoxResult entry in candidates)
    {
        var fronts = LaunchBoxParser.ParseFronts(await http.GetStringAsync(entry.ImagesUrl, refresh));
        BoxFront? pick = LaunchBoxParser.PickFront(fronts, c.JapanOnly);
        if (pick is not null)
        {
            Console.WriteLine($"  art: {pick.Region} {pick.Width}x{pick.Height} ({entry.IdSlug})");
            return (entry, pick, $"{entry.IdSlug} [{entry.Platform}]: {pick.Title}");
        }
        note += $"{fronts.Count} fronts on {entry.IdSlug}, none usable; ";
    }
    return (candidates[0], null, note);
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

// Finds every game page on the archive: the sitemap and a probe list seed a crawl that follows the
// /games/<slug> links on each game page. A slug only counts if its page has the labelled game fields.
async Task ListAsync()
{
    var queue = new Queue<string>();
    var seen = new HashSet<string>();
    void Add(string slug)
    {
        if (slug.Length > 0 && seen.Add(slug)) queue.Enqueue(slug);
    }
    foreach (string file in new[] { Path.Combine(root, "tools", "slugs-sitemap.txt"), Path.Combine(root, "data", "extra-slugs.txt"), Path.Combine(root, "data", "slugs.txt") })
        if (File.Exists(file)) foreach (string line in File.ReadAllLines(file)) Add(line.Trim());

    var games = new SortedDictionary<string, string>();
    var linkRx = new System.Text.RegularExpressions.Regex(@"href=""(?:https://www\.thesmackdownhotel\.com)?/games/([a-z0-9-]+)[""#/]");
    string[] notGames = ["arenas", "championships", "page-2", "page-3", "page-5"];
    int visited = 0;
    while (queue.Count > 0)
    {
        string slug = queue.Dequeue();
        if (notGames.Contains(slug)) continue;
        visited++;
        string html;
        try { html = await http.GetStringAsync("https://www.thesmackdownhotel.com/games/" + slug, refresh); }
        catch (HttpRequestException) { continue; }
        ArchiveFacts? facts = SmackdownParser.Parse(slug, html);
        if (facts is null) continue;
        games[slug] = facts.Title;
        foreach (System.Text.RegularExpressions.Match m in linkRx.Matches(html)) Add(m.Groups[1].Value);
        if (visited % 25 == 0) Console.WriteLine($"  visited {visited}, games {games.Count}, queue {queue.Count}");
    }
    File.WriteAllLines(Path.Combine(root, "data", "slugs.txt"), games.Keys);
    Console.WriteLine($"Found {games.Count} game pages ({visited} slugs tried). Wrote data/slugs.txt");
}

static string FindRoot()
{
    string? dir = AppContext.BaseDirectory;
    while (dir is not null && !Directory.GetFiles(dir, "*.sln").Any()) dir = Path.GetDirectoryName(dir);
    return dir ?? Directory.GetCurrentDirectory();
}
