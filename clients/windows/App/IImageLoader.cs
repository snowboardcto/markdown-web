using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TheMarkdownWeb.App;

/// <summary>
/// App-side image-load seam (Story 3.5 AC7). The ONLY place that performs image I/O — so the content
/// host's post-process is unit-testable with a stub (no socket, no decode). <see cref="Rendering"/>
/// stays pure: it records the source on <c>Image.Tag</c> and never loads bytes.
/// </summary>
public interface IImageLoader
{
    /// <summary>Loads the image at <paramref name="absolute"/>, or returns <c>null</c> on any failure.</summary>
    ImageSource? Load(Uri absolute);
}

/// <summary>
/// Default <see cref="IImageLoader"/>: builds a <see cref="BitmapImage"/> with the absolute
/// <c>UriSource</c> (WPF imaging, NOT an embedded browser). ALL load/decode failures are swallowed to
/// <c>null</c> so a broken image never crashes the app (the host then leaves the <c>Image</c> empty,
/// alt preserved). This is the single point of real image I/O.
/// </summary>
public sealed class SystemImageLoader : IImageLoader
{
    /// <inheritdoc />
    public ImageSource? Load(Uri absolute)
    {
        if (absolute is null)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = absolute;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }
            return bitmap;
        }
        catch
        {
            // Broken / 404 / decode-failure / unsupported scheme — never re-throw (AC7 / AC8 totality).
            return null;
        }
    }
}
