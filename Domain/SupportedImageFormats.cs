namespace PhotoQuick.Domain;

public static class SupportedImageFormats
{
    private static readonly HashSet<string> CommonExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"
    };

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nef", ".cr2", ".cr3", ".arw", ".rw2", ".orf"
    };

    public static bool IsSupported(string extension) =>
        CommonExtensions.Contains(extension) || RawExtensions.Contains(extension);

    public static bool IsRaw(string extension) => RawExtensions.Contains(extension);
}
