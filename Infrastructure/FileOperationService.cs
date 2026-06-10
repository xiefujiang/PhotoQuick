using System.IO;
using Microsoft.VisualBasic.FileIO;
using PhotoQuick.Domain;

namespace PhotoQuick.Infrastructure;

public sealed class FileOperationService : IFileOperationService
{
    public Task<RenameResult> RenameAsync(ImageItem item, string newBaseName, CancellationToken ct) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(newBaseName))
            {
                return new RenameResult(false, null, "新文件名不能为空。");
            }

            if (newBaseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return new RenameResult(false, null, "新文件名包含非法字符。");
            }

            var newPath = Path.Combine(item.DirectoryPath, newBaseName + item.Extension);
            if (File.Exists(newPath))
            {
                return new RenameResult(false, null, "目标文件名已存在。");
            }

            try
            {
                File.Move(item.Path, newPath);
                return new RenameResult(true, newPath, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new RenameResult(false, null, ex.Message);
            }
        }, ct);

    public Task<MoveResult> MoveAsync(ImageItem item, string targetFolder, CancellationToken ct) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                return new MoveResult(false, null, "请先选择移动目标目录。");
            }

            try
            {
                Directory.CreateDirectory(targetFolder);
                var targetPath = GetAvailableTargetPath(Path.Combine(targetFolder, item.FileName));
                File.Move(item.Path, targetPath);
                return new MoveResult(true, targetPath, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new MoveResult(false, null, ex.Message);
            }
        }, ct);

    public Task<DeleteResult> MoveToRecycleBinAsync(ImageItem item, CancellationToken ct) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                FileSystem.DeleteFile(
                    item.Path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                return new DeleteResult(true, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new DeleteResult(false, ex.Message);
            }
        }, ct);

    private static string GetAvailableTargetPath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
