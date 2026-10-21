using WrestlingGameMuseum.Core;

namespace WrestlingGameMuseum.Tests;

public class QueryTests
{
    private static Game G(string id, string title, string date, string platform, string series = "", string dev = "", params string[] promos) =>
        new()
        {
            Id = id, Title = title, Releases = [new Release("", date)], Platforms = [platform], Series = series,
            Developer = dev, Promotions = promos, Sources = [new SourceRef("t", "https://example.com")],
        };

    private static readonly Game[] Sample =
    [
        G("a", "Zeta Slam", "1999-05-01", "PlayStation", "Zeta", "Yuke's", "WWF"),
        G("b", "Alpha Bout", "1989-06-22", "PC Engine", "Alpha", "Human", "Independent Circuit"),
        G("c", "Beta Bout", "1993-12-28", "Super NES", "Alpha", "Human", "Independent Circuit"),
        G("d", "Gamma Mania", "2004", "PlayStation 2", "", "Yuke's", "WWE"),
    ];

    [Fact]
    public void DefaultSortIsByReleaseDate() =>
        Assert.Equal(["b", "c", "a", "d"], new GameQuery().Apply(Sample).Select(g => g.Id));

    [Fact]
    public void TitleSort() =>
        Assert.Equal(["Alpha Bout", "Beta Bout", "Gamma Mania", "Zeta Slam"],
            new GameQuery { Sort = SortOrder.Title }.Apply(Sample).Select(g => g.Title));

    [Fact]
    public void SearchMatchesTitleDeveloperAndPlatform()
    {
        Assert.Equal(["a"], new GameQuery { Search = "zeta" }.Apply(Sample).Select(g => g.Id));
        Assert.Equal(["a", "d"], new GameQuery { Search = "yuke" }.Apply(Sample).Select(g => g.Id));
        Assert.Equal(["d"], new GameQuery { Search = "playstation 2" }.Apply(Sample).Select(g => g.Id));
    }

    [Fact]
    public void FiltersCombine()
    {
        var q = new GameQuery { Era = "1990s", Series = "Alpha", Promotion = "Independent Circuit", Developer = "Human", Platform = "Super NES" };
        Assert.Equal(["c"], q.Apply(Sample).Select(g => g.Id));
    }

    [Fact]
    public void ErasShelveByDecadeInOrder()
    {
        var q = new GameQuery();
        var shelves = q.Shelves(q.Apply(Sample));
        Assert.Equal(["1980s", "1990s", "2000s"], shelves.Select(s => s.Name));
        Assert.Equal(2, shelves[1].Games.Count);
    }

    [Fact]
    public void SeriesShelvesPutStandAloneLast()
    {
        var q = new GameQuery { Group = Grouping.Series };
        Assert.Equal(["Alpha", "Zeta", "Stand-alone games"], q.Shelves(q.Apply(Sample)).Select(s => s.Name));
    }

    [Fact]
    public void FilterOptionsStartWithAllAndAreDistinct()
    {
        var eras = GameQuery.Options(Sample, g => [g.Era]);
        Assert.Equal(["All", "1980s", "1990s", "2000s"], eras);
    }

    [Theory]
    [InlineData(900, 3)] [InlineData(1179, 3)] [InlineData(1300, 4)] [InlineData(1920, 5)]
    public void ColumnsFollowWidth(double width, int columns) => Assert.Equal(columns, IndexLayout.ColumnsFor(width));

    [Fact]
    public void ImageAreaScalesDownButNeverAboveFull()
    {
        Assert.Equal(324, IndexLayout.ImageHeightFor(3000, 5));
        Assert.True(IndexLayout.ImageHeightFor(850, 3) < 324);
        Assert.True(IndexLayout.ImageHeightFor(850, 3) >= 160);
    }

    [Fact]
    public void LayoutBuildsBandsTileRowsAndFooter()
    {
        var q = new GameQuery();
        var rows = IndexLayout.Build(q.Shelves(q.Apply(Sample)), 3, 324, false);
        Assert.IsType<BandRow>(rows[0]);
        Assert.IsType<TileRow>(rows[1]);
        Assert.IsType<FooterRow>(rows[^1]);
        Assert.All(rows.OfType<TileRow>(), r => Assert.InRange(r.Games.Count, 1, 3));
    }

    [Fact]
    public void LayoutSplitsLongShelvesIntoRows()
    {
        var many = Enumerable.Range(0, 11).Select(i => G("g" + i, "Game " + i, "1995", "SNES")).ToList();
        var q = new GameQuery();
        var tileRows = IndexLayout.Build(q.Shelves(q.Apply(many)), 4, 324, false).OfType<TileRow>().ToList();
        Assert.Equal([4, 4, 3], tileRows.Select(r => r.Games.Count));
    }

    [Fact]
    public void NoMatchesShowsEmptyState()
    {
        var rows = IndexLayout.Build([], 3, 324, true);
        Assert.True(Assert.IsType<EmptyRow>(rows[0]).FiltersActive);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var catalog = new Catalog { Games = Sample };
        var again = CatalogLoader.Parse(CatalogLoader.Serialize(catalog));
        Assert.Equal(Sample.Select(g => g.Id), again.Games.Select(g => g.Id));
        Assert.Equal("WWF", again.Games[0].Promotions[0]);
    }
}
