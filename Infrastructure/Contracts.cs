using PhotoQuick.Domain;

namespace PhotoQuick.Infrastructure;

public interface IFolderScanner
{
    IAsyncEnumerable<ScanProgress> ScanAsync(string folder, bool recursive, CancellationToken ct);
}

public interface IImageDecoder
{
    Task<DecodedImage> DecodePreviewAsync(ImageItem item, CancellationToken ct);
    Task<DecodedImage> DecodeFullAsync(ImageItem item, CancellationToken ct);
}

public interface IImageCache
{
    bool TryGet(string path, out DecodedImage image);
    void Put(string path, DecodedImage image);
    void KeepOnlyAround(IReadOnlyList<ImageItem> items, int currentIndex, int radius);
    void Clear();
}

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct);
    Task SaveAsync(AppSettings settings, CancellationToken ct);
}

public interface IFileOperationService
{
    Task<RenameResult> RenameAsync(ImageItem item, string newBaseName, CancellationToken ct);
    Task<MoveResult> MoveAsync(ImageItem item, string targetFolder, CancellationToken ct);
    Task<DeleteResult> MoveToRecycleBinAsync(ImageItem item, CancellationToken ct);
}
