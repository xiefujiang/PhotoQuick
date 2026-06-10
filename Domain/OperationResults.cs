namespace PhotoQuick.Domain;

public sealed record RenameResult(bool Success, string? NewPath, string? Error);
public sealed record MoveResult(bool Success, string? NewPath, string? Error);
public sealed record DeleteResult(bool Success, string? Error);
