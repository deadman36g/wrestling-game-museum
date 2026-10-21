using System.Net;
using System.Text.RegularExpressions;

namespace Harvester;

public sealed record ArchiveFacts(
    string Slug,
    string Title,
    string ReleaseDate,
    string AdditionalReleases,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Publishers,
    IReadOnlyList<string> Developers,
    IReadOnlyList<string> Promotions,
    IReadOnlyList<string> Series,
    string Description)
{
    public string PageUrl => "https://www.thesmackdownhotel.com/games/" + Slug;
}

/// <summary>Reads the labelled fields of a SmackDown Hotel game page. Its images are banners and are never used as box art.</summary>
public static partial class SmackdownParser
{
    [GeneratedRegex(@"<li class=""field-entry[^""]*"">\s*<span class=""field-label\s*"">([^<]*)</span>\s*<span class=""field-value\s*"">(.*?)</span>\s*</li>", RegexOptions.Singleline)]
    private static partial Regex FieldRx();
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();
    [GeneratedRegex(@"<title>(.*?)\s*\|", RegexOptions.Singleline)] private static partial Regex TitleRx();
    [GeneratedRegex(@"<h1[^>]*>\s*(?:<[^>]+>\s*)*([^<]+)", RegexOptions.Singleline)] private static partial Regex H1Rx();
    [GeneratedRegex(@"</ul>\s*</div>\s*<p>(.*?)</p>", RegexOptions.Singleline)] private static partial Regex DescRx();
    [GeneratedRegex(@"\s+")] private static partial Regex WsRx();
    [GeneratedRegex(@"(\d)(st|nd|rd|th)\b")] private static partial Regex OrdinalRx();

    /// <summary>Each linked value of a multi-value field (a game can have several developers, promotions, platforms).</summary>
    public static IReadOnlyList<string> Values(string fieldHtml)
    {
        var linked = Regex.Matches(fieldHtml, @"<a [^>]*>(.*?)</a>", RegexOptions.Singleline)
            .Select(x => Text(x.Groups[1].Value)).Where(x => x.Length > 0).Distinct().ToList();
        if (linked.Count > 0) return linked;
        string plain = Text(fieldHtml);
        return plain.Length == 0 ? [] : [plain];
    }

    public static string Text(string html) =>
        WsRx().Replace(WebUtility.HtmlDecode(TagRx().Replace(html, " ")), " ").Trim();

    public static ArchiveFacts? Parse(string slug, string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in FieldRx().Matches(html))
            fields.TryAdd(Text(m.Groups[1].Value), m.Groups[2].Value);
        if (fields.Count == 0) return null;

        string title = Text(TitleRx().Match(html) is { Success: true } t ? t.Groups[1].Value : "");
        if (title.Length == 0 && H1Rx().Match(html) is { Success: true } h) title = Text(h.Groups[1].Value);
        if (title.Length == 0) return null;

        IReadOnlyList<string> Many(string key) => fields.TryGetValue(key, out string? v) ? Values(v) : [];
        string F(string key) => fields.TryGetValue(key, out string? v) ? Text(v) : "";
        var platforms = Many("Platforms");
        string desc = DescRx().Match(html) is { Success: true } d ? Text(d.Groups[1].Value) : "";
        return new ArchiveFacts(slug, title, F("Release Date"), F("Additional Releases"), platforms,
            Many("Publisher"), Many("Developer"), Many("Promotion"), Many("Game Series"), desc);
    }

    /// <summary>"December 22, 1995" / "March 29th, 1996" / "1996" → "1995-12-22" / "1996-03-29" / "1996"; null if unreadable.</summary>
    public static string? NormalizeDate(string text)
    {
        string t = OrdinalRx().Replace(text.Trim(), "$1");
        string[] formats = ["MMMM d, yyyy", "MMM d, yyyy"];
        if (DateTime.TryParseExact(t, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
            return d.ToString("yyyy-MM-dd");
        if (DateTime.TryParseExact(t, ["MMMM yyyy", "MMM yyyy"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var m))
            return m.ToString("yyyy-MM");
        return Regex.IsMatch(t, @"^(19|20)\d\d$") ? t : null;
    }
}
