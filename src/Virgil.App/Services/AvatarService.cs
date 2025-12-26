using System;
using System.IO;
using Virgil.Domain;

namespace Virgil.App.Services;

public static class AvatarService
{
    public static string GetAvatarPath(Mood mood)
    {
        var name = mood switch
        {
            Mood.Neutral => "virgil_normal.png",
            Mood.Happy => "virgil_normal.png",
            Mood.Focused => "virgil_normal.png",
            Mood.Warn => "virgil_stress.png",
            Mood.Alert => "virgil_critical.png",
            _ => "virgil_normal.png"
        };

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, "assets", "virgil", "static", "256", name),
            Path.Combine(baseDir, "assets", "virgil", "static", "64", name),
            Path.Combine(baseDir, "assets", "virgil", "static", "1024", name),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        // Fallback sur la version normale si l’asset demandé est manquant
        var fallbackCandidates = new[]
        {
            Path.Combine(baseDir, "assets", "virgil", "static", "256", "virgil_normal.png"),
            Path.Combine(baseDir, "assets", "virgil", "static", "64", "virgil_normal.png"),
            Path.Combine(baseDir, "assets", "virgil", "static", "1024", "virgil_normal.png"),
        };

        foreach (var fallback in fallbackCandidates)
        {
            if (File.Exists(fallback)) return fallback;
        }

        return string.Empty;
    }
}
