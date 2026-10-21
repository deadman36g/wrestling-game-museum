using System.Diagnostics;

namespace WrestlingGameMuseum.Services;

public static class UrlLauncher
{
    /// <summary>Opens an https link in the visitor's browser. Returns false for anything else.</summary>
    public static bool TryOpen(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }
}
