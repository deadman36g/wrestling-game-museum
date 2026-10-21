namespace Harvester;

/// <summary>How well a LaunchBox title matches the archive's title: 3 exact, 2 close, 1 same-title-plus-subtitle, 0 no.</summary>
public static class TitleMatch
{
    private static readonly string[] Drop = ["the", "a", "of"];

    public static string[] Tokens(string title) =>
        new string(title.ToLowerInvariant().Replace("&", " and ").Replace("nihon", "nippon").Replace("vs.", "vs")
                .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !Drop.Contains(t)).ToArray();

    public static int Score(string launchBoxTitle, params string[] wanted)
    {
        string[] lb = Tokens(launchBoxTitle);
        int best = 0;
        foreach (string w in wanted)
        {
            string[] want = Tokens(w);
            if (want.Length == 0) continue;
            if (lb.SequenceEqual(want)) return 3;
            double jaccard = (double)lb.Intersect(want).Count() / lb.Union(want).Count();
            if (jaccard >= 0.75 && !lb.Except(want).Concat(want.Except(lb)).Any(t => t.Any(char.IsDigit) || IsRoman(t))) best = Math.Max(best, 2);
            // "Wrestle Kingdom 2" vs "Wrestle Kingdom 2 Pro Wrestling Sekai Taisen": same title with a subtitle,
            // but never "Fire Pro Wrestling" vs "Fire Pro Wrestling 2" (a different game).
            if (lb.Length > want.Length && lb.Take(want.Length).SequenceEqual(want) && !lb[want.Length].Any(char.IsDigit)
                && !IsRoman(lb[want.Length]))
                best = Math.Max(best, 1);
        }
        return best;
    }

    private static bool IsRoman(string t) => t.Length <= 4 && t.All(c => "ivxl".Contains(c));
}
