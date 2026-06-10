using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoQuick.Domain;

namespace PhotoQuick.Infrastructure;

public sealed class WicImageDecoder : IImageDecoder
{
    private const int PreviewPixelWidth = 4096;

    public Task<DecodedImage> DecodePreviewAsync(ImageItem item, CancellationToken ct) =>
        Task.Run(() => Decode(item, PreviewPixelWidth, ct), ct);

    public Task<DecodedImage> DecodeFullAsync(ImageItem item, CancellationToken ct) =>
        Task.Run(() => Decode(item, 0, ct), ct);

    private static DecodedImage Decode(ImageItem item, int decodePixelWidth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth > 0)
            {
                bitmap.DecodePixelWidth = decodePixelWidth;
            }

            bitmap.UriSource = new Uri(item.Path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var bytesPerPixel = Math.Max(1, bitmap.Format.BitsPerPixel / 8);
            var estimate = (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
            return new DecodedImage(bitmap, estimate);
        }
        catch
        {
            return CreatePlaceholder();
        }
    }

    private static DecodedImage CreatePlaceholder()
    {
        const int width = 960;
        const int height = 640;
        const int stride = width * 4;
        var pixels = new byte[height * stride];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var shade = (byte)(((x / 24 + y / 24) % 2 == 0) ? 42 : 52);
                pixels[offset] = shade;
                pixels[offset + 1] = shade;
                pixels[offset + 2] = shade;
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return new DecodedImage(bitmap, pixels.Length, isPlaceholder: true);
    }
}
