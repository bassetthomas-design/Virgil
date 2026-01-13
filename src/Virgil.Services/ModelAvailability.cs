using System;
using System.IO;
using System.Linq;
using Virgil.Core.Config;
using Virgil.Services.Assistant;

namespace Virgil.Services;

public sealed record ModelAvailabilityResult(
    bool IsModelFilePresent,
    bool IsRuntimePresent,
    bool IsHashProvided,
    bool? IsHashVerified,
    bool CanRunOffline,
    string UserMessage,
    string ModelPath);

public static class ModelAvailability
{
    public static ModelAvailabilityResult Check(
        ModelLocator? modelLocator = null,
        ModelPackManifest? manifest = null,
        string? runtimePath = null)
    {
        var locator = modelLocator ?? new ModelLocator();
        var resolvedManifest = manifest ?? ModelPackManifest.FullPack;

        var modelPresent = locator.TryResolve(out var resolvedPath, out _);
        var modelPath = modelPresent ? resolvedPath : locator.GetCandidatePaths().First();

        var runtimeExpectedPath = string.IsNullOrWhiteSpace(runtimePath)
            ? LlamaRuntimeManager.DefaultRuntimePath
            : runtimePath;
        var runtimePresent = File.Exists(runtimeExpectedPath);

        var hashProvided = !string.IsNullOrWhiteSpace(resolvedManifest.Sha256);
        bool? hashVerified = null;

        if (modelPresent && hashProvided)
        {
            var hashPath = locator.GetHashPathForModel(modelPath);
            if (File.Exists(hashPath))
            {
                var actualHash = File.ReadAllText(hashPath).Trim();
                if (!string.IsNullOrWhiteSpace(actualHash))
                {
                    hashVerified = string.Equals(
                        resolvedManifest.Sha256.Trim(),
                        actualHash,
                        StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    hashVerified = false;
                }
            }
        }

        var canRunOffline = modelPresent && runtimePresent;
        var userMessage = ResolveUserMessage(modelPresent, hashProvided, hashVerified);

        return new ModelAvailabilityResult(
            modelPresent,
            runtimePresent,
            hashProvided,
            hashVerified,
            canRunOffline,
            userMessage,
            modelPath);
    }

    private static string ResolveUserMessage(bool modelPresent, bool hashProvided, bool? hashVerified)
    {
        if (!modelPresent)
        {
            return "Modèle manquant";
        }

        if (hashVerified == false)
        {
            return "Hash incorrect.";
        }

        if (!hashProvided || hashVerified is null)
        {
            return "Modèle installé (hash non vérifié)";
        }

        return "Modèle: installé";
    }
}
