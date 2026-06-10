using PhotoQuick.Domain;
using PhotoQuick.Infrastructure;

namespace PhotoQuick.Application;

public sealed class BrowserSession
{
    private readonly IImageDecoder _decoder;
    private readonly IImageCache _cache;
    private CancellationTokenSource? _navCts;

    public BrowserSession(IImageDecoder decoder, IImageCache cache)
    {
        _decoder = decoder;
        _cache = cache;
    }

    public IReadOnlyList<ImageItem> Items { get; private set; } = [];
    public int CurrentIndex { get; private set; } = -1;

    public void SetItems(IReadOnlyList<ImageItem> items)
    {
        _navCts?.Cancel();
        _cache.Clear();
        Items = items;
        CurrentIndex = items.Count > 0 ? 0 : -1;
    }

    public async Task<DecodedImage?> NavigateAsync(int nextIndex)
    {
        if (Items.Count == 0)
        {
            return null;
        }

        _navCts?.Cancel();
        _navCts = new CancellationTokenSource();
        var token = _navCts.Token;

        CurrentIndex = Math.Clamp(nextIndex, 0, Items.Count - 1);
        var item = Items[CurrentIndex];

        if (!_cache.TryGet(item.Path, out var image))
        {
            image = await _decoder.DecodePreviewAsync(item, token);
            _cache.Put(item.Path, image);
        }

        _cache.KeepOnlyAround(Items, CurrentIndex, radius: 1);
        _ = PreloadAroundAsync(CurrentIndex, token);
        return image;
    }

    private async Task PreloadAroundAsync(int index, CancellationToken ct)
    {
        foreach (var i in new[] { index - 1, index + 1 })
        {
            if (i < 0 || i >= Items.Count || ct.IsCancellationRequested)
            {
                continue;
            }

            var item = Items[i];
            if (_cache.TryGet(item.Path, out _))
            {
                continue;
            }

            try
            {
                var image = await _decoder.DecodePreviewAsync(item, ct);
                _cache.Put(item.Path, image);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
