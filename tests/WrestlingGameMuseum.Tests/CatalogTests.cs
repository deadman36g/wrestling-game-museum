using System.Text.RegularExpressions;
using WrestlingGameMuseum.Core;

namespace WrestlingGameMuseum.Tests;

/// <summary>Schema and consistency rules for data/games.json. These guard against invented or half-filled records.</summary>
public class CatalogTests
{
    private static readonly Catalog Data = CatalogLoader.Load(RepoPaths.Path("data", "games.json"));
    private static readonly Regex DateRx = new(@"^(19|20)\d\d(-(0[1-9]|1[0-2])(-(0[1-9]|[12]\d|3[01]))?)?$");
    public static IEnumerable<object[]> Games => Data.Games.Select(g => new object[] { g.Id });

    [Fact] public void CatalogIsNotEmpty() => Assert.NotEmpty(Data.Games);

    [Fact]
    public void IdsAreUniqueSlugs()
    {
        Assert.Equal(Data.Games.Count, Data.Games.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Data.Games, g => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", g.Id));
    }

    [Fact]
    public void TitlesAreUniquePerPlatformSet()
    {
        var dupes = Data.Games.GroupBy(g => g.Title.ToLowerInvariant() + "|" + string.Join(",", g.Platforms.OrderBy(p => p)))
            .Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        Assert.Empty(dupes);
    }

    [Fact]
    public void EveryRecordHasTitleYearPlatformAndSource()
    {
        foreach (Game g in Data.Games)
        {
            Assert.False(string.IsNullOrWhiteSpace(g.Title), g.Id + ": title");
            Assert.True(g.Year >= 1970 && g.Year <= DateTime.UtcNow.Year + 1, g.Id + ": year " + g.Year);
            Assert.NotEmpty(g.Platforms);
            Assert.NotEmpty(g.Sources);
        }
    }

    [Fact]
    public void ReleaseDatesAreWellFormed()
    {
        foreach (Game g in Data.Games)
            foreach (Release r in g.Releases)
                Assert.Matches(DateRx, r.Date);
    }

    [Fact]
    public void AllLinksAreHttps()
    {
        foreach (Game g in Data.Games)
        {
            var urls = g.Sources.Select(s => s.Url)
                .Concat(g.Screenshots.SelectMany(m => new[] { m.SourceUrl, m.ThumbnailUrl }))
                .Concat(g.Videos.SelectMany(m => new[] { m.SourceUrl, m.ThumbnailUrl }))
                .Concat(g.Regional.Select(r => r.BoxArtUrl))
                .Append(g.RosterSourceUrl).Append(g.BoxArt?.Url).Append(g.BoxArt?.SourcePage)
                .Where(u => !string.IsNullOrEmpty(u));
            foreach (string? url in urls)
                Assert.True(Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps, $"{g.Id}: {url}");
        }
    }

    [Fact]
    public void BoxArtIsCreditedAndNeverTheArchiveBanner()
    {
        foreach (Game g in Data.Games.Where(g => g.BoxArt is not null))
        {
            Assert.False(string.IsNullOrEmpty(g.BoxArt!.Credit), g.Id + ": box art needs a credit");
            Assert.False(string.IsNullOrEmpty(g.BoxArt.SourcePage), g.Id + ": box art needs its source page");
            Assert.DoesNotContain("thesmackdownhotel.com/images", g.BoxArt.Url);
            Assert.True(g.BoxArt.Height == 0 || g.BoxArt.Width / (double)g.BoxArt.Height < 1.4, g.Id + ": box art looks like a banner");
        }
    }

    [Fact]
    public void EveryFactHasASourceThatNamesIt()
    {
        foreach (Game g in Data.Games)
            Assert.All(g.Sources, s => Assert.False(string.IsNullOrWhiteSpace(s.Label), g.Id));
    }

    [Fact]
    public void WrittenTextIsPlainAndModest()
    {
        string[] banned = ["best-selling", "bestselling", "best selling", "greatest wrestling game"];
        foreach (Game g in Data.Games)
            foreach (string text in new[] { g.Significance, g.History, g.Features })
                Assert.DoesNotContain(banned, b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DateFormatting()
    {
        Assert.Equal("December 22, 1995", Game.FormatDate("1995-12-22"));
        Assert.Equal("March 1996", Game.FormatDate("1996-03"));
        Assert.Equal("1996", Game.FormatDate("1996"));
    }
}
