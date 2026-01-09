namespace Virgil.Core.Models;

public enum DownloadStatus
{
    Success,
    Failed,
    Canceled,
}

public sealed record DownloadResult(DownloadStatus Status, string Message, string? FilePath = null)
{
    public bool Success => Status == DownloadStatus.Success;

    public static DownloadResult Succeeded(string path) =>
        new(DownloadStatus.Success, "Téléchargement terminé", path);

    public static DownloadResult Failed(string message) =>
        new(DownloadStatus.Failed, message);

    public static DownloadResult Canceled(string message = "Téléchargement annulé") =>
        new(DownloadStatus.Canceled, message);
}
