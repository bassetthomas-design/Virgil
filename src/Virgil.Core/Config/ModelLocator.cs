using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Virgil.Core.Logging;

namespace Virgil.Core.Config;

public sealed class ModelLocator
{
    private readonly string _expectedFileName;

    public ModelLocator(string? expectedFileName = null)
    {
        _expectedFileName = string.IsNullOrWhiteSpace(expectedFileName)
            ? GetExpectedFileName()
            : expectedFileName;
    }

    public string ExpectedFileName => _expectedFileName;

    public string PreferredModelDirectory => Path.Combine(AppContext.BaseDirectory, "AI", "Models");

    public string FallbackModelDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Virgil",
        "AI",
        "Models");

    public string ModelDirectory => PreferredModelDirectory;

    public string ModelPath => Path.Combine(ModelDirectory, ExpectedFileName);

    public string ModelHashPath => Path.Combine(ModelDirectory, $"{ExpectedFileName}.sha256");

    public string GetHashPathForModel(string modelPath)
    {
        var directory = Path.GetDirectoryName(modelPath);
        return string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(ModelDirectory, $"{ExpectedFileName}.sha256")
            : Path.Combine(directory, $"{ExpectedFileName}.sha256");
    }

    public IEnumerable<string> GetCandidatePaths()
    {
        yield return Path.Combine(PreferredModelDirectory, ExpectedFileName);
        yield return Path.Combine(FallbackModelDirectory, ExpectedFileName);
    }

    public bool TryResolve(out string path, out string reason)
    {
        foreach (var candidate in GetCandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            path = candidate;
            var age = GetModelAge(path);
            var ageText = FormatAge(age);
            reason = $"Modèle installé: {path} (âge: {ageText}).";
            Log.Info($"Modèle GGUF utilisé: {path} (âge: {ageText}).");
            return true;
        }

        path = GetCandidatePaths().First();
        reason = $"Modèle IA manquant: {path}";
        Log.Warn($"Modèle GGUF manquant. Attendu: {string.Join(" | ", GetCandidatePaths())}");
        return false;
    }

    public bool IsInstalled => GetCandidatePaths().Any(File.Exists);

    public static string GetExpectedFileName()
    {
        var manifest = ModelPackManifest.FullPack;
        if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl)
            && Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return $"{manifest.DisplayName}.gguf";
    }

    public static TimeSpan GetModelAge(string modelPath)
    {
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(modelPath);
    }

    public static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
        {
            return $"{(int)age.TotalDays}j {age.Hours}h";
        }

        if (age.TotalHours >= 1)
        {
            return $"{(int)age.TotalHours}h {age.Minutes}m";
        }

        if (age.TotalMinutes >= 1)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        var seconds = Math.Max(0, (int)age.TotalSeconds);
        return $"{seconds}s";
    }
}
