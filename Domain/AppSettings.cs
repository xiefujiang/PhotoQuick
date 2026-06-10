namespace PhotoQuick.Domain;

public sealed class AppSettings
{
    public bool RecursiveScan { get; set; }
    public SortMode SortMode { get; set; } = SortMode.FileNameAsc;
    public bool ShowSidePanel { get; set; } = true;
    public List<string> PresetMoveFolders { get; set; } = [];

    public static AppSettings Default => new()
    {
        RecursiveScan = false,
        SortMode = SortMode.FileNameAsc,
        ShowSidePanel = true,
        PresetMoveFolders = []
    };
}
