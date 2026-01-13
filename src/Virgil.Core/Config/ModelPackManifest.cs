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
        "Meta-Llama-3.1-8B-Instruct-Q5_K_M",
        "https://huggingface.co/TheBloke/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf",
        string.Empty,
        null);
}
