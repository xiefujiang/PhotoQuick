using System.IO;

namespace PhotoQuick.Domain;

public sealed record ImageItem(
    string Path,
    string Name,
    string Extension,
    long SizeBytes,
    DateTime LastWriteTime,
    bool IsRaw)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public string SizeText => FormatBytes(SizeBytes);
    public string LastWriteTimeText => LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string KindText => IsRaw ? "RAW" : Extension.TrimStart('.').ToUpperInvariant();

    public ImageItem WithPath(string newPath)
    {
        var info = new FileInfo(newPath);
        return this with
        {
            Path = info.FullName,
            Name = System.IO.Path.GetFileNameWithoutExtension(info.Name),
            Extension = info.Extension,
            SizeBytes = info.Exists ? info.Length : SizeBytes,
            LastWriteTime = info.Exists ? info.LastWriteTime : LastWriteTime,
            IsRaw = SupportedImageFormats.IsRaw(info.Extension)
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
