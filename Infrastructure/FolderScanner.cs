using System.IO;
using System.Runtime.CompilerServices;
using PhotoQuick.Domain;

namespace PhotoQuick.Infrastructure;

public sealed class FolderScanner : IFolderScanner
{
    public async IAsyncEnumerable<ScanProgress> ScanAsync(
        string folder,
        bool recursive,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var count = 0;

        foreach (var path in EnumerateFilesSafe(folder, recursive))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();

            var extension = Path.GetExtension(path);
            if (!SupportedImageFormats.IsSupported(extension))
            {
                continue;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            count++;
            yield return new ScanProgress(
                count,
                new ImageItem(
                    info.FullName,
                    Path.GetFileNameWithoutExtension(info.Name),
                    info.Extension,
                    info.Length,
                    info.LastWriteTime,
                    SupportedImageFormats.IsRaw(info.Extension)));
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, bool recursive)
    {
        var folders = new Stack<string>();
        folders.Push(root);

        while (folders.Count > 0)
        {
            var folder = folders.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (!recursive)
            {
                continue;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                folders.Push(child);
            }
        }
    }
}
