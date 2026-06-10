using PhotoQuick.Domain;

namespace PhotoQuick.Infrastructure;

public sealed class ImageMemoryCache : IImageCache
{
    private readonly Dictionary<string, DecodedImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly long _maxBytes;
    private long _currentBytes;

    public ImageMemoryCache(long maxBytes = 512L * 1024 * 1024)
    {
        _maxBytes = maxBytes;
    }

    public bool TryGet(string path, out DecodedImage image) => _images.TryGetValue(path, out image!);

    public void Put(string path, DecodedImage image)
    {
        if (_images.Remove(path, out var existing))
        {
            _currentBytes -= existing.EstimatedBytes;
            existing.Dispose();
        }

        _images[path] = image;
        _currentBytes += image.EstimatedBytes;
        TrimToLimit();
    }

    public void KeepOnlyAround(IReadOnlyList<ImageItem> items, int currentIndex, int radius)
    {
        var keep = items
            .Select((item, index) => new { item.Path, index })
            .Where(x => Math.Abs(x.index - currentIndex) <= radius)
            .Select(x => x.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _images.Keys.ToArray())
        {
            if (!keep.Contains(key))
            {
                Remove(key);
            }
        }
    }

    public void Clear()
    {
        foreach (var image in _images.Values)
        {
            image.Dispose();
        }

        _images.Clear();
        _currentBytes = 0;
    }

    private void TrimToLimit()
    {
        foreach (var key in _images.Keys.ToArray())
        {
            if (_currentBytes <= _maxBytes)
            {
                break;
            }

            Remove(key);
        }
    }

    private void Remove(string key)
    {
        if (!_images.Remove(key, out var image))
        {
            return;
        }

        _currentBytes -= image.EstimatedBytes;
        image.Dispose();
    }
}
