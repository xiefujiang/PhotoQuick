using System.Windows.Media.Imaging;

namespace PhotoQuick.Infrastructure;

public sealed class DecodedImage : IDisposable
{
    public DecodedImage(BitmapSource bitmap, long estimatedBytes, bool isPlaceholder = false)
    {
        Bitmap = bitmap;
        EstimatedBytes = estimatedBytes;
        IsPlaceholder = isPlaceholder;
    }

    public BitmapSource Bitmap { get; }
    public long EstimatedBytes { get; }
    public bool IsPlaceholder { get; }

    public void Dispose()
    {
        // BitmapImage is loaded with CacheOption.OnLoad, so file streams are closed before caching.
        // Native RAW handles should be disposed here when LibRaw integration replaces the bridge.
    }
}
