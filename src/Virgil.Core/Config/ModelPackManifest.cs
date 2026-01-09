namespace Virgil.Core.Config;

public sealed record ModelPackManifest(
    string PackId,
    string DisplayName,
    string DownloadUrl)
{
    public string? PackSha256 { get; init; }

    public long? PackSizeBytes { get; init; }

    public static ModelPackManifest FullPack { get; } = new(
        "pack-full-llama31-8b",
        "llama-3.1-8b-instruct-q4_k_m",
        "https://huggingface.co/TheBloke/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/llama-3.1-8b-instruct-q4_k_m.gguf");

    public static string? Sha256 => FullPack.PackSha256;

    public static long? SizeBytes => FullPack.PackSizeBytes;
}
