namespace WrestlingGameMuseum.Core;

/// <summary>
/// One item of the museum floor. The index is a flat list of rows (a band, then rows of tiles) so a virtualising
/// list can scroll hundreds of games smoothly: only the visible rows ever get containers.
/// </summary>
public abstract record IndexRow;

public sealed record BandRow(string Title, string Note) : IndexRow;

public sealed record TileRow(IReadOnlyList<Game> Games, int Columns, double ImageHeight) : IndexRow;

public sealed record EmptyRow(bool FiltersActive) : IndexRow;

public sealed record FooterRow : IndexRow;

public static class IndexLayout
{
    public const int MinColumns = 3, MaxColumns = 5;
    public const double TileGap = 10, FullImageHeight = 324, FullTileWidth = 340;

    /// <summary>Three to five tiles across depending on the width available.</summary>
    public static int ColumnsFor(double width) => width switch
    {
        < 1180 => MinColumns,
        < 1560 => 4,
        _ => MaxColumns,
    };

    /// <summary>The image area is 324 tall at full tile width and scales down with narrower tiles (rounded to 4px so resizing is cheap).</summary>
    public static double ImageHeightFor(double width, int columns)
    {
        double tileWidth = (width - TileGap * (columns - 1)) / columns;
        double scaled = FullImageHeight * Math.Min(1, Math.Max(0.5, tileWidth / FullTileWidth));
        return Math.Round(scaled / 4) * 4;
    }

    public static IReadOnlyList<IndexRow> Build(IReadOnlyList<Shelf> shelves, int columns, double imageHeight, bool filtersActive)
    {
        var rows = new List<IndexRow>();
        if (shelves.Count == 0) rows.Add(new EmptyRow(filtersActive));
        foreach (Shelf shelf in shelves)
        {
            rows.Add(new BandRow(shelf.Name, shelf.Games.Count == 1 ? "1 game" : shelf.Games.Count + " games"));
            for (int i = 0; i < shelf.Games.Count; i += columns)
                rows.Add(new TileRow(shelf.Games.Skip(i).Take(columns).ToList(), columns, imageHeight));
        }
        rows.Add(new FooterRow());
        return rows;
    }
}
