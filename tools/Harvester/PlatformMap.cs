namespace Harvester;

/// <summary>Lines up the archive's platform names with LaunchBox's so a search hit for the wrong platform is not taken as the game.</summary>
public static class PlatformMap
{
    private static readonly (string Archive, string LaunchBox)[] Pairs =
    [
        ("PlayStation 1", "Sony Playstation"), ("PlayStation 2", "Sony Playstation 2"), ("PlayStation 3", "Sony Playstation 3"),
        ("PlayStation 4", "Sony Playstation 4"), ("PlayStation 5", "Sony Playstation 5"), ("PSP", "Sony PSP"), ("PS Vita", "Sony PS Vita"),
        ("Super NES", "Super Nintendo Entertainment System"), ("NES", "Nintendo Entertainment System"), ("Nintendo 64", "Nintendo 64"),
        ("GameCube", "Nintendo GameCube"), ("Wii", "Nintendo Wii"), ("Wii U", "Nintendo Wii U"), ("Switch", "Nintendo Switch"),
        ("Game Boy Advance", "Nintendo Game Boy Advance"), ("Game Boy Color", "Nintendo Game Boy Color"), ("Game Boy", "Nintendo Game Boy"),
        ("Nintendo DS", "Nintendo DS"), ("Nintendo 3DS", "Nintendo 3DS"),
        ("Xbox", "Microsoft Xbox"), ("Xbox 360", "Microsoft Xbox 360"), ("Xbox One", "Microsoft Xbox One"), ("Xbox Series X", "Microsoft Xbox Series X/S"),
        ("Dreamcast", "Sega Dreamcast"), ("SEGA Genesis", "Sega Genesis"), ("SEGA Saturn", "Sega Saturn"), ("SEGA Master System", "Sega Master System"),
        ("SEGA Game Gear", "Sega Game Gear"), ("PC Windows", "Windows"), ("PC", "Windows"), ("TurboGrafx-16 (PC Engine)", "NEC TurboGrafx-16"),
        ("Arcade", "Arcade"),
    ];

    private static string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>True when the LaunchBox platform label is the same system as the archive platform.</summary>
    public static bool Matches(string archivePlatform, string launchBoxPlatform)
    {
        string a = Norm(archivePlatform), l = Norm(launchBoxPlatform);
        if (a == l) return true;
        foreach (var (arch, lb) in Pairs)
            if (Norm(arch) == a && Norm(lb) == l) return true;
        return false;
    }

    /// <summary>0 = best (first listed platform) … higher is worse; -1 when no listed platform matches.</summary>
    public static int Rank(IReadOnlyList<string> archivePlatforms, string launchBoxPlatform)
    {
        for (int i = 0; i < archivePlatforms.Count; i++)
            if (Matches(archivePlatforms[i], launchBoxPlatform)) return i;
        return -1;
    }
}
