using System.Windows;
using System.Windows.Controls;

namespace WrestlingGameMuseum.Services;

/// <summary>
/// Attached properties that fill an Image from a credited URL via <see cref="ImageCache"/>.
/// Virtualised tiles reuse containers, so a late result for an old URL is discarded.
/// </summary>
public static class CachedImage
{
    public static readonly DependencyProperty UrlProperty = DependencyProperty.RegisterAttached(
        "Url", typeof(string), typeof(CachedImage), new PropertyMetadata(null, OnUrlChanged));

    public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.RegisterAttached(
        "DecodeHeight", typeof(int), typeof(CachedImage), new PropertyMetadata(640));

    public static string? GetUrl(DependencyObject d) => (string?)d.GetValue(UrlProperty);
    public static void SetUrl(DependencyObject d, string? value) => d.SetValue(UrlProperty, value);
    public static int GetDecodeHeight(DependencyObject d) => (int)d.GetValue(DecodeHeightProperty);
    public static void SetDecodeHeight(DependencyObject d, int value) => d.SetValue(DecodeHeightProperty, value);

    private static async void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        image.Source = null;
        if (e.NewValue is not string { Length: > 0 } url) return;
        var bitmap = await ImageCache.LoadAsync(url, GetDecodeHeight(image));
        if (bitmap is not null && GetUrl(image) == url) image.Source = bitmap;
    }
}
