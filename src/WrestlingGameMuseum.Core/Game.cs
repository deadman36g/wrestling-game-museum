using System.Text.Json.Serialization;

namespace WrestlingGameMuseum.Core;

/// <summary>One release of a game in one region. Date is "yyyy-MM-dd", "yyyy-MM" or just "yyyy".</summary>
public sealed record Release(string Region, string Date, string Platform = "");

public sealed record BoxArt(
    string Url,
    string Region,
    string SourcePage,
    string Credit,
    int Width = 0,
    int Height = 0);

public sealed record RegionalRelease(string Region, string Title, string Date, string Platform = "", string BoxArtUrl = "");

public sealed record MediaLink(string Title, string SourceUrl, string ThumbnailUrl = "", string Credit = "");

public sealed record Fact(string Label, string Value);

public sealed record RosterEntry(string Name, string RealName = "", string Group = "", string Note = "");

/// <summary>Where a fact came from. Every fact in a record must be traceable to one of these.</summary>
public sealed record SourceRef(string Label, string Url, string Facts = "");

public sealed record Game
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<string> AlternateTitles { get; init; } = [];
    public IReadOnlyList<Release> Releases { get; init; } = [];
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public string Developer { get; init; } = "";
    public string Publisher { get; init; } = "";
    public IReadOnlyList<string> Promotions { get; init; } = [];
    public string Series { get; init; } = "";
    public string Significance { get; init; } = "";
    public string History { get; init; } = "";
    public string Features { get; init; } = "";
    public BoxArt? BoxArt { get; init; }
    public IReadOnlyList<RegionalRelease> Regional { get; init; } = [];
    public IReadOnlyList<MediaLink> Screenshots { get; init; } = [];
    public IReadOnlyList<MediaLink> Videos { get; init; } = [];
    public string RosterSourceUrl { get; init; } = "";
    public IReadOnlyList<RosterEntry> Roster { get; init; } = [];
    public IReadOnlyList<SourceRef> Sources { get; init; } = [];

    /// <summary>The earliest release date string (sorts correctly as text for the supported formats).</summary>
    [JsonIgnore]
    public string FirstDate => Releases.Count == 0 ? "" : Releases.Select(r => r.Date).Where(d => d.Length >= 4).OrderBy(d => d, StringComparer.Ordinal).FirstOrDefault() ?? "";

    [JsonIgnore]
    public int Year => FirstDate.Length >= 4 && int.TryParse(FirstDate.AsSpan(0, 4), out int y) ? y : 0;

    [JsonIgnore]
    public string Era => Year == 0 ? "Undated" : (Year / 10 * 10) + "s";

    [JsonIgnore]
    public string YearText => Year == 0 ? "Undated" : Year.ToString();

    /// <summary>Small gold line above the title: series, then developer.</summary>
    [JsonIgnore]
    public string Eyebrow => string.Join(" · ", new[] { Series, Developer }.Where(s => s.Length > 0));

    /// <summary>The fact chips shown on the exhibit. Only facts the catalog actually has are listed.</summary>
    [JsonIgnore]
    public IReadOnlyList<Fact> Facts => new[]
    {
        new Fact("Released", FirstDate.Length == 0 ? "" : FormatDate(FirstDate)),
        new Fact(Platforms.Count == 1 ? "Platform" : "Platforms", string.Join(", ", Platforms)),
        new Fact("Developer", Developer),
        new Fact("Publisher", Publisher),
        new Fact(Promotions.Count == 1 ? "Promotion" : "Promotions", string.Join(", ", Promotions)),
    }.Where(f => f.Value.Length > 0).ToList();

    [JsonIgnore]
    public bool HasRegional => Regional.Count > 0;

    [JsonIgnore]
    public bool HasScreenshots => Screenshots.Count > 0;

    [JsonIgnore]
    public bool HasVideos => Videos.Count > 0;

    [JsonIgnore]
    public string PlatformText => string.Join(" / ", Platforms);

    [JsonIgnore]
    public bool HasBoxArt => BoxArt is { Url.Length: > 0 };

    /// <summary>A human-readable date such as "December 22, 1995", "March 1996" or "1996".</summary>
    public static string FormatDate(string date)
    {
        if (DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d))
            return d.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
        if (DateTime.TryParseExact(date, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var m))
            return m.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return date;
    }
}

public sealed record Catalog
{
    public int Version { get; init; } = 1;
    public string Generated { get; init; } = "";
    public IReadOnlyList<Game> Games { get; init; } = [];
}
