namespace Virgil.Core.Config;

public sealed record ModelPackManifest(
    string PackId,
    string FileName,
    string DownloadUrl,
    string Sha256,
    long? SizeBytes)
{
    public static ModelPackManifest FullPack { get; } = new(
        PackId: "full",
        FileName: ModelLocator.ExpectedFileName,
        DownloadUrl: string.Empty,
        Sha256: string.Empty,
        SizeBytes: null);
}
