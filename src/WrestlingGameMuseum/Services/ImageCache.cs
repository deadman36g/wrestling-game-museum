using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace WrestlingGameMuseum.Services;

/// <summary>
/// Streams artwork from its credited source and keeps a copy under %LOCALAPPDATA%\WrestlingGameMuseum\cache
/// so it is only downloaded once. Nothing is bundled in the app. Bitmaps are decoded at display size
/// (box scans can be 4000px tall) and frozen so they can be built off the UI thread.
/// </summary>
public static class ImageCache
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WrestlingGameMuseum/0.1";
    private static readonly SemaphoreSlim Gate = new(4);
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, BitmapImage> Memory = new();
    private static readonly object MemoryLock = new();

    public static string CacheDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WrestlingGameMuseum", "cache");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    public static string FileFor(string url) =>
        Path.Combine(CacheDirectory, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant() + ".img");

    /// <summary>Returns a frozen bitmap, or null when the image cannot be fetched (the tile keeps its placeholder).</summary>
    public static async Task<BitmapImage?> LoadAsync(string url, int decodeHeight)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        string key = decodeHeight + "|" + url;
        lock (MemoryLock)
            if (Memory.TryGetValue(key, out BitmapImage? hit)) return hit;

        await Gate.WaitAsync();
        try
        {
            string file = FileFor(url);
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(CacheDirectory);
                byte[] bytes = await Http.GetByteArrayAsync(uri);
                string temp = file + "." + Environment.ProcessId + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes);
                File.Move(temp, file, overwrite: true);
            }
            BitmapImage image = await Task.Run(() => Decode(file, decodeHeight));
            lock (MemoryLock)
            {
                if (Memory.Count > 300) Memory.Clear();
                Memory[key] = image;
            }
            return image;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException
                                       or NotSupportedException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
        finally { Gate.Release(); }
    }

    private static BitmapImage Decode(string file, int decodeHeight)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelHeight = decodeHeight;
        image.UriSource = new Uri(file);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
