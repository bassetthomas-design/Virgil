namespace Virgil.Core.Diagnostics;

using Virgil.Core.Config;
using Virgil.Core.Logging;

public sealed class StartupDiagnostics
{
    private readonly string _appDataPath;
    private readonly string _assetsPath;
    private readonly string _logPath;
    private readonly IReadOnlyList<(string Description, string Path)> _requiredAssets;

    public StartupDiagnostics(string? appDataPath = null)
    {
        var resolvedAppDataPath = appDataPath ?? AppPaths.UserDataRoot;

        _appDataPath = TrimTrailingSeparators(resolvedAppDataPath);
        _assetsPath = Path.Combine(_appDataPath, "assets");
        _logPath = Log.CurrentLogFile;

        _requiredAssets = new List<(string Description, string Path)>
        {
            ("Textures Virgil (256px)", Path.Combine(_assetsPath, "virgil", "static", "256", "virgil_normal.png")),
            ("Textures Virgil (stress)", Path.Combine(_assetsPath, "virgil", "static", "256", "virgil_stress.png")),
            ("Textures Virgil (critical)", Path.Combine(_assetsPath, "virgil", "static", "256", "virgil_critical.png")),
            ("Mapping des processus", Path.Combine(_assetsPath, "activity", "process-map.json")),
            ("Voix FR - system.json", Path.Combine(_assetsPath, "voice", "fr", "system.json")),
            ("Voix FR - moods.json", Path.Combine(_assetsPath, "voice", "fr", "moods.json")),
            ("Voix FR - actions.json", Path.Combine(_assetsPath, "voice", "fr", "actions.json")),
            ("Modèle GGUF", Path.Combine(_assetsPath, "models", "virgil-model.gguf")),
            ("Prompt système", Path.Combine(_assetsPath, "prompts", "system_prompt.txt")),
        };
    }

    public StartupStatus Run()
    {
        var missing = new List<string>();

        foreach (var asset in _requiredAssets)
        {
            if (!File.Exists(asset.Path))
            {
                missing.Add($"{asset.Description} manquant (attendu : {asset.Path})");
            }
        }

        if (missing.Count == 0)
        {
            Log.Info("Démarrage: tous les prérequis sont présents.");
        }
        else
        {
            Log.Warn($"Démarrage en mode dégradé: {string.Join(", ", missing)}");
        }

        return new StartupStatus(missing.Count == 0, missing, _logPath);
    }

    private static string TrimTrailingSeparators(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

public sealed record StartupStatus(bool IsReady, IReadOnlyList<string> MissingItems, string LogPath)
{
    public string Summary => IsReady
        ? "Tous les assets requis sont présents."
        : "Mode dégradé: certaines ressources sont absentes.";
}
