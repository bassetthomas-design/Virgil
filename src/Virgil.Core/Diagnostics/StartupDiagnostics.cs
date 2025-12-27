namespace Virgil.Core.Diagnostics;

using Virgil.Core.Logging;

public sealed class StartupDiagnostics
{
    private readonly string _appDataPath;
    private readonly string _assetsPath;
    private readonly string _logPath;

    public StartupDiagnostics(string? appDataPath = null)
    {
        var resolvedAppDataPath = appDataPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Virgil");

        _appDataPath = TrimTrailingSeparators(resolvedAppDataPath);
        _assetsPath = Path.Combine(_appDataPath, "assets");
        _logPath = Path.Combine(_appDataPath, "logs", $"{DateTime.Now:yyyy-MM-dd}.log");
    }

    public StartupStatus Run()
    {
        var missing = new List<string>();
        var avatarPath = Path.Combine(_assetsPath, "avatar.png");
        var modelPath = Path.Combine(_assetsPath, "model.gguf");
        var promptPath = Path.Combine(_assetsPath, "system-prompt.txt");

        if (!File.Exists(avatarPath))
        {
            missing.Add($"Avatar manquant (attendu : {avatarPath})");
        }

        if (!File.Exists(modelPath))
        {
            missing.Add($"Modèle GGUF manquant (attendu : {modelPath})");
        }

        if (!File.Exists(promptPath))
        {
            missing.Add($"Prompt système manquant (attendu : {promptPath})");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
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
