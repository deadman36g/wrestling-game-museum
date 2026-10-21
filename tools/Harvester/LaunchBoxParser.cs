using System.Net;
using System.Text.RegularExpressions;

namespace Harvester;

public sealed record LaunchBoxResult(string IdSlug, string Title, string Platform)
{
    public string ImagesUrl => "https://gamesdb.launchbox-app.com/games/images/" + IdSlug;
    public string DetailsUrl => "https://gamesdb.launchbox-app.com/games/details/" + IdSlug;
}

public sealed record BoxFront(string Url, string Title, string Region, int Width, int Height)
{
    public double Ratio => Height == 0 ? 0 : (double)Width / Height;
}

public static partial class LaunchBoxParser
{
    [GeneratedRegex(@"href=""/games/details/([^""]+)"".*?<h3[^>]*>(.*?)</h3>\s*<p[^>]*>(.*?)</p>", RegexOptions.Singleline)]
    private static partial Regex ResultRx();
    [GeneratedRegex(@"<a [^>]*href=""(https://images\.launchbox-app\.com/[^""]+)""[^>]*data-title=""([^""]*Box - Front[^""]*)""[^>]*data-footer=""(\d+) x (\d+)", RegexOptions.Singleline)]
    private static partial Regex FrontRx();
    [GeneratedRegex(@"\(([^)]+)\)\s*$")] private static partial Regex RegionRx();

    public static string SearchUrl(string query) =>
        "https://gamesdb.launchbox-app.com/games/results/" + Uri.EscapeDataString(query.Replace('/', ' '));

    public static IReadOnlyList<LaunchBoxResult> ParseSearch(string html) =>
        ResultRx().Matches(html)
            .Select(m => new LaunchBoxResult(m.Groups[1].Value,
                WebUtility.HtmlDecode(m.Groups[2].Value.Trim()), WebUtility.HtmlDecode(m.Groups[3].Value.Trim())))
            .DistinctBy(r => r.IdSlug).ToList();

    public static IReadOnlyList<BoxFront> ParseFronts(string html) =>
        FrontRx().Matches(html).Select(m =>
        {
            string title = WebUtility.HtmlDecode(m.Groups[2].Value);
            string region = RegionRx().Match(title) is { Success: true } r ? r.Groups[1].Value : "Unknown";
            return new BoxFront(m.Groups[1].Value, title, region, int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value));
        }).DistinctBy(f => f.Url).ToList();

    /// <summary>
    /// Chooses a real box scan. Wide banners and tiny images (logos) are rejected.
    /// Japan-only games get the Japanese box; everything else prefers North America, then Europe.
    /// </summary>
    public static BoxFront? PickFront(IEnumerable<BoxFront> fronts, bool japanOnly)
    {
        string[] order = japanOnly
            ? ["Japan", "North America", "Europe", "World", "Unknown"]
            : ["North America", "Europe", "World", "Australia", "Japan", "Unknown"];
        var usable = fronts.Where(IsRealBox).ToList();
        foreach (string region in order)
            if (usable.Where(f => f.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                      .OrderByDescending(f => f.Height).FirstOrDefault() is { } hit)
                return hit;
        return usable.OrderByDescending(f => f.Height).FirstOrDefault();
    }

    public static bool IsRealBox(BoxFront f) =>
        f.Width >= 180 && f.Height >= 240 && f.Ratio is >= 0.45 and <= 1.35;
}
