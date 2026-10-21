namespace WrestlingGameMuseum.Tests;

internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir, "WrestlingGameMuseum.sln")))
            dir = System.IO.Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Repository root (WrestlingGameMuseum.sln) not found.");
    }
}
