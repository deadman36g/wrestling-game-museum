using WrestlingGameMuseum.Core;

namespace Harvester;

public static class CatalogBuilder
{
    public const string ArchiveLabel = "The SmackDown Hotel game archive";
    public const string ArtLabel = "LaunchBox Games Database";

    public static Game Build(CurationEntry c, ArchiveFacts? facts, LaunchBoxResult? lbEntry, BoxFront? front)
    {
        var sources = new List<SourceRef>();
        if (facts is not null)
            sources.Add(new SourceRef(ArchiveLabel, facts.PageUrl, "Release date, platforms, developer, publisher, promotion, series"));

        var releases = c.Releases.Count > 0 ? c.Releases : ReleasesFrom(facts);
        var platforms = c.Platforms.Count > 0 ? c.Platforms : facts?.Platforms ?? [];

        foreach (Correction fix in c.Corrections)
            sources.Add(new SourceRef("Correction to archive: " + fix.Field, fix.SourceUrl, fix.Note));
        sources.AddRange(c.ExtraSources);

        BoxArt? art = null;
        if (front is not null && lbEntry is not null)
        {
            art = new BoxArt(front.Url, front.Region, lbEntry.ImagesUrl, ArtLabel, front.Width, front.Height);
            sources.Add(new SourceRef(ArtLabel + " (box art)", lbEntry.ImagesUrl, front.Title));
        }

        return new Game
        {
            Id = c.Id.Length > 0 ? c.Id : c.Slug,
            Title = c.Title ?? facts?.Title ?? c.Slug,
            AlternateTitles = c.AlternateTitles,
            Releases = releases,
            Platforms = platforms,
            Developer = c.Developer ?? string.Join(", ", facts?.Developers ?? []),
            Publisher = c.Publisher ?? string.Join(", ", facts?.Publishers ?? []),
            Promotions = c.Promotions.Count > 0 ? c.Promotions : facts?.Promotions ?? [],
            Series = c.Series ?? SeriesFrom(facts),
            Significance = c.Significance,
            History = c.History,
            Features = c.Features,
            BoxArt = art,
            Screenshots = c.Screenshots,
            Videos = c.Videos,
            RosterSourceUrl = c.RosterSourceUrl,
            Sources = sources,
        };
    }

    // The archive's "Game Series" field also carries category tags that are not series.
    private static readonly string[] NotSeries = ["Japanese", "Independent Games"];

    private static string SeriesFrom(ArchiveFacts? facts) =>
        facts?.Series.FirstOrDefault(s => !NotSeries.Contains(s, StringComparer.OrdinalIgnoreCase)) ?? "";

    private static IReadOnlyList<Release> ReleasesFrom(ArchiveFacts? facts)
    {
        if (facts is null) return [];
        string? date = SmackdownParser.NormalizeDate(facts.ReleaseDate);
        return date is null ? [] : [new Release("", date)];
    }
}
