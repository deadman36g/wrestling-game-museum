using System.Text.Json;
using WrestlingGameMuseum.Core;

namespace Harvester;

/// <summary>Hand-written, sourced additions and corrections layered over what the archives say. Lives in data/curation.json.</summary>
public sealed record CurationEntry
{
    public string Slug { get; init; } = "";
    public string Id { get; init; } = "";
    public string? Title { get; init; }
    public IReadOnlyList<string> AlternateTitles { get; init; } = [];
    public string LaunchBoxQuery { get; init; } = "";
    /// <summary>Pins the LaunchBox entry ("28949-super-fire-pro-wrestling-x") when the search would pick the wrong one.</summary>
    public string LaunchBoxId { get; init; } = "";
    public bool JapanOnly { get; init; }
    /// <summary>Set when the only art on LaunchBox is a logo/banner that slipped past the shape check; the tile shows a placeholder.</summary>
    public string NoBoxArtReason { get; init; } = "";
    public string? Series { get; init; }
    public string Significance { get; init; } = "";
    public string History { get; init; } = "";
    public string Features { get; init; } = "";
    public string RosterSourceUrl { get; init; } = "";
    /// <summary>When the archive is wrong, say what and why; the correction is also recorded in sources.</summary>
    public IReadOnlyList<Correction> Corrections { get; init; } = [];
    public IReadOnlyList<SourceRef> ExtraSources { get; init; } = [];
    public IReadOnlyList<MediaLink> Screenshots { get; init; } = [];
    public IReadOnlyList<MediaLink> Videos { get; init; } = [];
    public IReadOnlyList<Release> Releases { get; init; } = [];
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public string? Developer { get; init; }
    public string? Publisher { get; init; }
    public IReadOnlyList<string> Promotions { get; init; } = [];
}

public sealed record Correction(string Field, string Note, string SourceUrl);

public sealed record CurationFile(IReadOnlyList<CurationEntry> Games)
{
    public static CurationFile Load(string path) =>
        JsonSerializer.Deserialize<CurationFile>(File.ReadAllText(path), CatalogLoader.Options) ?? new CurationFile([]);
}
