namespace Virgil.Core.Config;

public sealed record ModelPackManifest(
    string PackId,
    string DisplayName,
    string DownloadUrl,
    string Sha256,
    long? SizeBytes)
{
    public static ModelPackManifest FullPack { get; } = new(
        "pack-full-llama31-8b",
        "llama-3.1-8b-instruct-q4_k_m",
        "https://huggingface.co/TheBloke/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/llama-3.1-8b-instruct-q4_k_m.gguf",
        string.Empty,
        null);
}
