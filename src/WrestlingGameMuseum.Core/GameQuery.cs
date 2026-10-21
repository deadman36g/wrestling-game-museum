namespace WrestlingGameMuseum.Core;

public enum SortOrder { ReleaseDate, Title }

public enum Grouping { Era, Series, Developer }

public sealed record Shelf(string Name, IReadOnlyList<Game> Games);

/// <summary>Everything the index needs: free-text search, dropdown filters, sorting and shelf grouping.</summary>
public sealed record GameQuery
{
    public const string Any = "All";

    public string Search { get; init; } = "";
    public string Era { get; init; } = Any;
    public string Platform { get; init; } = Any;
    public string Promotion { get; init; } = Any;
    public string Developer { get; init; } = Any;
    public string Series { get; init; } = Any;
    public SortOrder Sort { get; init; } = SortOrder.ReleaseDate;
    public Grouping Group { get; init; } = Grouping.Era;

    public IReadOnlyList<Game> Apply(IEnumerable<Game> games)
    {
        string term = Search.Trim();
        IEnumerable<Game> q = games.Where(g =>
            Is(Era, g.Era) &&
            (Platform == Any || g.Platforms.Contains(Platform, StringComparer.OrdinalIgnoreCase)) &&
            (Promotion == Any || g.Promotions.Contains(Promotion, StringComparer.OrdinalIgnoreCase)) && Is(Developer, g.Developer) && Is(Series, g.Series) &&
            (term.Length == 0 || Matches(g, term)));
        return (Sort == SortOrder.Title
            ? q.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            : q.OrderBy(g => g.Year == 0 ? int.MaxValue : g.Year).ThenBy(g => g.FirstDate, StringComparer.Ordinal)
               .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<Shelf> Shelves(IReadOnlyList<Game> sorted)
    {
        Func<Game, string> key = Group switch
        {
            Grouping.Series => g => g.Series.Length == 0 ? "Stand-alone games" : g.Series,
            Grouping.Developer => g => g.Developer.Length == 0 ? "Developer unknown" : g.Developer,
            _ => g => g.Era,
        };
        var shelves = sorted.GroupBy(key).Select(grp => new Shelf(grp.Key, grp.ToList())).ToList();
        // Era shelves follow the calendar; named shelves read alphabetically with catch-alls last.
        return Group == Grouping.Era
            ? shelves.OrderBy(s => s.Name == "Undated" ? 1 : 0).ThenBy(s => s.Name, StringComparer.Ordinal).ToList()
            : shelves.OrderBy(s => s.Name.StartsWith("Stand-alone") || s.Name.EndsWith("unknown") ? 1 : 0)
                     .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> Options(IEnumerable<Game> games, Func<Game, IEnumerable<string>> pick) =>
        new[] { Any }.Concat(games.SelectMany(pick).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)).ToList();

    private static bool Is(string filter, string value) =>
        filter == Any || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(Game g, string term) =>
        g.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        g.AlternateTitles.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
        g.Series.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        g.Developer.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        g.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        g.Platforms.Any(p => p.Contains(term, StringComparison.OrdinalIgnoreCase));
}
