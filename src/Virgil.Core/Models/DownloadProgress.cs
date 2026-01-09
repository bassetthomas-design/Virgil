namespace Virgil.Core.Models;

public sealed record DownloadProgress(long BytesDownloaded, long? TotalBytes, double? Percent, double SpeedBytesPerSec);
