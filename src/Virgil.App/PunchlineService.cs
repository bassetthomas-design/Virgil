using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Virgil.App
{
    /// <summary>
    /// Charge des punchlines depuis %ProgramData%\Virgil\chat.json et %AppData%\Virgil\chat.json
    /// (l'utilisateur override la machine). Fallback sur une petite liste embarquée.
    /// </summary>
    public static class PunchlineService
    {
        private static readonly string MachinePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "chat.json");
        private static readonly string UserPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "chat.json");

        private static readonly string[] Fallback =
        {
            "Routine OK. Tous les systèmes au vert.",
            "Je veille sur tes températures.",
            "Un petit nettoyage plus tard ?",
            "Winget est prêt à upgrader ce qui traîne.",
            "Si tu chauffes, je te préviens. Promis.",
            "Un scan Defender rapide ?"
        };

        private static string[] _lines = Fallback;

        static PunchlineService()
        {
            TryLoad();
        }

        private static void TryLoad()
        {
            try
            {
                string[] machine = File.Exists(MachinePath)
                    ? ReadJsonLines(MachinePath)
                    : Array.Empty<string>();
                string[] user = File.Exists(UserPath)
                    ? ReadJsonLines(UserPath)
                    : Array.Empty<string>();

                // Fusion machine + user (user override = on met d'abord machine, puis user)
                var merged = machine.Concat(user).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
                if (merged.Length > 0) _lines = merged;
            }
            catch
            {
                // garde Fallback
            }
        }

        private static string[] ReadJsonLines(string path)
        {
            using var fs = File.OpenRead(path);
            var arr = JsonSerializer.Deserialize<string[]>(fs);
            return arr ?? Array.Empty<string>();
        }

        public static string RandomBanter()
        {
            if (_lines.Length == 0) return Fallback[Random.Shared.Next(Fallback.Length)];
            return _lines[Random.Shared.Next(_lines.Length)];
        }
    }
}
