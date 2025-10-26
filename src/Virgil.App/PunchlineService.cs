using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Virgil.App
{
    /// <summary>
    /// Charge des phrases (punchlines/humeurs/infos) depuis JSON :
    ///  - %ProgramData%/Virgil/punchlines.json
    ///  - %AppData%/Virgil/punchlines.user.json  (override/ajouts)
    /// Fallback : liste intégrée.
    /// Format JSON attendu : { "lines": ["texte 1", "texte 2", ...] }
    /// </summary>
    public sealed class PunchlineService
    {
        private readonly string _machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "punchlines.json");
        private readonly string _user    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "punchlines.user.json");

        private readonly string[] _fallback = new[]
        {
            "Je veille. Rien ne m’échappe.",
            "Hydrate-toi, pas que le CPU.",
            "Un petit nettoyage et ça repart.",
            "Winget est prêt à tout casser (dans le bon sens).",
            "Je fais tourner les ventilos… dans ma tête.",
            "Si ça rame, c’est pas un bateau.",
            "On efface et on recommence ?"
        };

        private string[] _lines;

        public PunchlineService()
        {
            _lines = Load();
            if (_lines.Length == 0) _lines = _fallback;
        }

        private string[] Load()
        {
            var all = new List<string>();
            foreach (var path in new[] { _machine, _user })
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var json = JsonSerializer.Deserialize<PunchlinesFile>(File.ReadAllText(path),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (json?.Lines != null) all.AddRange(json.Lines.Where(s => !string.IsNullOrWhiteSpace(s)));
                    }
                }
                catch { /* safe */ }
            }
            return all.Distinct().ToArray();
        }

        public string Random()
        {
            if (_lines.Length == 0) _lines = _fallback;
            var r = new Random();
            return _lines[r.Next(_lines.Length)];
        }

        private sealed class PunchlinesFile { public string[]? Lines { get; set; } }
    }
}
