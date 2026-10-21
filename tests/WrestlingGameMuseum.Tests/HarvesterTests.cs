using Harvester;

namespace WrestlingGameMuseum.Tests;

/// <summary>The parsers run against small hand-trimmed excerpts shaped like the real pages.</summary>
public class HarvesterTests
{
    private const string ArchiveExcerpt = """
        <html><head><title>Super Fire Pro Wrestling X | WWE Games &amp; Wrestling Games Database</title></head><body>
        <h1>Super Fire Pro Wrestling X</h1>
        <div><ul class="fields-container">
        <li class="field-entry promotions promotion"> <span class="field-label ">Promotion</span>
        <span class="field-value ">
        <a href="/promotions/a" class="multiple-values-line"><img src="x.webp" alt="a">All Japan Pro Wrestling</a><a href="/promotions/b" class="multiple-values-line">New Japan Pro Wrestling</a></span></li>
        <li class="field-entry series "> <span class="field-label">Game Series</span><span class="field-value "><span class="fire-pro"><a href="/games/#x" title="Game Series: Fire Pro Wrestling">Fire Pro Wrestling</a></span></span></li>
        <li class="field-entry release-date release-date">
        <span class="field-label ">Release Date</span>
        <span class="field-value ">
        December 22, 1995</span></li>
        <li class="field-entry additional-releases "> <span class="field-label">Additional Releases</span><span class="field-value ">March 29th, 1996 (As "Super Fire Pro Wrestling X Premium")</span></li>
        <li class="field-entry platforms platforms"> <span class="field-label">Platforms</span><span class="field-value "><span class="snes"><a href="/games/#p" title="Platforms: Super NES">Super NES</a></span></span></li>
        <li class="field-entry publisher "> <span class="field-label">Publisher</span><span class="field-value "><span class="he"><a href="/games/#q">Human Entertainment</a></span></span></li>
        <li class="field-entry developer "> <span class="field-label">Developer</span><span class="field-value "><span class="he"><a href="/games/#r">Human Entertainment</a></span></span></li></ul>
        </div>
        <p>A jam-packed installment of the series.</p><h2>Game Modes</h2>
        </body></html>
        """;

    [Fact]
    public void ParsesArchiveFields()
    {
        ArchiveFacts facts = SmackdownParser.Parse("super-fire-pro-wrestling-x", ArchiveExcerpt)!;
        Assert.Equal("Super Fire Pro Wrestling X", facts.Title);
        Assert.Equal("December 22, 1995", facts.ReleaseDate);
        Assert.Equal(["Super NES"], facts.Platforms);
        Assert.Equal(["All Japan Pro Wrestling", "New Japan Pro Wrestling"], facts.Promotions);
        Assert.Equal(["Human Entertainment"], facts.Developers);
        Assert.Equal(["Fire Pro Wrestling"], facts.Series);
        Assert.Contains("Premium", facts.AdditionalReleases);
        Assert.Equal("https://www.thesmackdownhotel.com/games/super-fire-pro-wrestling-x", facts.PageUrl);
    }

    [Fact] public void PageWithoutFieldsIsRejected() => Assert.Null(SmackdownParser.Parse("x", "<html><title>Nope | site</title></html>"));

    [Theory]
    [InlineData("December 22, 1995", "1995-12-22")]
    [InlineData("March 29th, 1996", "1996-03-29")]
    [InlineData("June 1st, 1989", "1989-06-01")]
    [InlineData("March 1996", "1996-03")]
    [InlineData("2001", "2001")]
    [InlineData("soon", null)]
    public void NormalizesDates(string text, string? expected) => Assert.Equal(expected, SmackdownParser.NormalizeDate(text));

    private const string LaunchBoxSearch = """
        <a class="list-item link-no-underline" href="/games/details/28949-super-fire-pro-wrestling-x">
        <div class="cardHeading"><div class="cardTitle"><h3 b-x>Super Fire Pro Wrestling X</h3><p b-x>Super Nintendo Entertainment System</p></div></div></a>
        <a class="list-item link-no-underline" href="/games/details/5879-super-fire-pro-wrestling-x-premium">
        <div class="cardHeading"><div class="cardTitle"><h3 b-x>Super Fire Pro Wrestling X Premium</h3><p b-x>Super Nintendo Entertainment System</p></div></div></a>
        """;

    [Fact]
    public void ParsesLaunchBoxSearchCards()
    {
        var results = LaunchBoxParser.ParseSearch(LaunchBoxSearch);
        Assert.Equal(2, results.Count);
        Assert.Equal("28949-super-fire-pro-wrestling-x", results[0].IdSlug);
        Assert.Equal("Super Nintendo Entertainment System", results[0].Platform);
        Assert.Equal("https://gamesdb.launchbox-app.com/games/images/28949-super-fire-pro-wrestling-x", results[0].ImagesUrl);
    }

    private const string LaunchBoxImages = """
        <a style="x" href="https://images.launchbox-app.com/jp.jpg" data-gameimageid="1" data-toggle="lightbox" data-title="Game - Box - Front Image (Japan)" data-footer="2419 x 4437 JPEG, 3.96 MB</br>">
        <a style="x" href="https://images.launchbox-app.com/na.jpg" data-gameimageid="2" data-toggle="lightbox" data-title="Game - Box - Front Image (North America)" data-footer="800 x 1100 JPEG">
        <a style="x" href="https://images.launchbox-app.com/banner.jpg" data-gameimageid="3" data-toggle="lightbox" data-title="Game - Box - Front Image (Europe)" data-footer="1200 x 400 JPEG">
        <a style="x" href="https://images.launchbox-app.com/logo.png" data-gameimageid="4" data-toggle="lightbox" data-title="Game - Clear Logo Image" data-footer="500 x 200 PNG">
        """;

    [Fact]
    public void ParsesOnlyBoxFronts()
    {
        var fronts = LaunchBoxParser.ParseFronts(LaunchBoxImages);
        Assert.Equal(["Japan", "North America", "Europe"], fronts.Select(f => f.Region));
        Assert.Equal(4437, fronts[0].Height);
    }

    [Fact]
    public void JapanOnlyGamesGetTheJapaneseBox() =>
        Assert.Equal("Japan", LaunchBoxParser.PickFront(LaunchBoxParser.ParseFronts(LaunchBoxImages), japanOnly: true)!.Region);

    [Fact]
    public void OtherGamesPreferNorthAmericaAndBannersAreRejected()
    {
        var fronts = LaunchBoxParser.ParseFronts(LaunchBoxImages);
        Assert.Equal("North America", LaunchBoxParser.PickFront(fronts, japanOnly: false)!.Region);
        Assert.Null(LaunchBoxParser.PickFront(fronts.Where(f => f.Region == "Europe"), japanOnly: false));
    }

    [Fact]
    public void CacheKeysAreStableAndFileSafe()
    {
        string key = PoliteHttp.CacheKey("https://gamesdb.launchbox-app.com/games/results/Fire Pro?x=1");
        Assert.Equal(key, PoliteHttp.CacheKey("https://gamesdb.launchbox-app.com/games/results/Fire Pro?x=1"));
        Assert.DoesNotContain(Path.GetInvalidFileNameChars(), c => key.Contains(c));
    }
}
