using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Virgil.Services.ModelPacks;

public sealed record ModelPackReferenceManifest(
    [property: JsonPropertyName("model_path")] string? ModelPath,
    [property: JsonPropertyName("model_sha256_expected")] string? ModelSha256Expected,
    [property: JsonPropertyName("runtime_sha256_expected")] string? RuntimeSha256Expected)
{
    public static bool TryLoad(string path, out ModelPackReferenceManifest? manifest, out string? error)
    {
        manifest = null;
        error = null;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            manifest = JsonSerializer.Deserialize<ModelPackReferenceManifest>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            return manifest is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
