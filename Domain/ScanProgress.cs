namespace PhotoQuick.Domain;

public sealed record ScanProgress(int Count, ImageItem? Item, string? Warning = null);
