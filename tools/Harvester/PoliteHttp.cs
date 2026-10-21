using System.Security.Cryptography;
using System.Text;

namespace Harvester;

/// <summary>
/// Low-concurrency HTTP with a real browser-style User-Agent (some of the sites reject bare tool agents),
/// retry with backoff (LaunchBox sometimes drops connections) and an on-disk cache of raw pages.
/// </summary>
public sealed class PoliteHttp : IDisposable
{
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 WrestlingGameMuseum/0.1";

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(2);
    private readonly string _cacheDir;
    private readonly TimeSpan _delay;

    public PoliteHttp(string cacheDir, TimeSpan? delay = null)
    {
        _cacheDir = cacheDir;
        _delay = delay ?? TimeSpan.FromMilliseconds(400);
        Directory.CreateDirectory(cacheDir);
        _http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        { Timeout = TimeSpan.FromSeconds(45) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public static string CacheKey(string url)
    {
        string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..12].ToLowerInvariant();
        var uri = new Uri(url);
        string stem = new string((uri.Host + uri.AbsolutePath).Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return (stem.Length > 80 ? stem[..80] : stem) + "." + hash;
    }

    public async Task<string> GetStringAsync(string url, bool refresh = false, int attempts = 5)
    {
        string path = Path.Combine(_cacheDir, CacheKey(url) + ".html");
        if (!refresh && File.Exists(path)) return await File.ReadAllTextAsync(path);

        await _gate.WaitAsync();
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await Task.Delay(_delay);
                    using HttpResponseMessage res = await _http.GetAsync(url);
                    if ((int)res.StatusCode is 404 or 410) throw new HttpRequestException($"{(int)res.StatusCode} {url}", null, res.StatusCode);
                    res.EnsureSuccessStatusCode();
                    string body = await res.Content.ReadAsStringAsync();
                    await File.WriteAllTextAsync(path, body);
                    return body;
                }
                catch (Exception ex) when (attempt < attempts && !IsPermanent(ex))
                {
                    TimeSpan wait = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.Error.WriteLine($"  retry {attempt}/{attempts - 1} in {wait.TotalSeconds}s: {url} ({ex.Message})");
                    await Task.Delay(wait);
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<byte[]> GetBytesAsync(string url, int attempts = 5)
    {
        string path = Path.Combine(_cacheDir, CacheKey(url) + ".bin");
        if (File.Exists(path)) return await File.ReadAllBytesAsync(path);
        await _gate.WaitAsync();
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await Task.Delay(_delay);
                    byte[] bytes = await _http.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(path, bytes);
                    return bytes;
                }
                catch (Exception ex) when (attempt < attempts && !IsPermanent(ex))
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }
        }
        finally { _gate.Release(); }
    }

    private static bool IsPermanent(Exception ex) =>
        ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone };

    public void Dispose() => _http.Dispose();
}
