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
            Mood.Happy => "happy.png",
            Mood.Focused => "focused.png",
            Mood.Warn => "warn.png",
            Mood.Alert => "alert.png",
            Mood.Sleepy => "sleepy.png",
            Mood.Tired => "tired.png",
            Mood.Proud => "proud.png",
            Mood.Angry => "angry.png",
            Mood.Playful => "playful.png",
            _ => "neutral.png"
        };

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(baseDir, "assets", "avatar", name);
        if (File.Exists(candidate)) return candidate;

        // Fallback sur neutral si l’asset demandé est manquant
        var fallback = Path.Combine(baseDir, "assets", "avatar", "neutral.png");
        return File.Exists(fallback) ? fallback : string.Empty;
    }
}
