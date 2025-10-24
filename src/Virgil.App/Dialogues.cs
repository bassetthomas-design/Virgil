#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Virgil.App
{
    /// <summary>
    /// Charge "virgil-dialogues.json" (même dossier que l'exe) et fournit des helpers
    /// pour éviter les répétitions (on n'utilise pas deux fois de suite la même phrase).
    ///
    /// Format attendu (exemple):
    /// {
    ///   "startup": ["Salut !", "Prêt à aider."],
    ///   "surveillance_start": ["Je veille.", "Surveillance activée."],
    ///   "surveillance_stop": ["Pause surveillance.", "Je me repose."],
    ///   "time_morning": ["Bonjour !", "Prêt pour la matinée ?"],
    ///   "time_afternoon": ["Toujours là.", "On continue."],
    ///   "time_evening": ["Belle soirée.", "On garde le cap."],
    ///   "time_night": ["Douce nuit.", "Je veille discrètement."],
    ///   "alert_temp": ["Attention, température élevée.", "Ça chauffe, prudence !"],
    ///   "action_maintenance_quick_start": ["Je prépare une maintenance rapide."],
    ///   "action_clean_temp_start": ["Je nettoie les fichiers temporaires."],
    ///   ...
    /// }
    /// </summary>
    internal static class Dialogues
    {
        private static readonly JsonObject _root;
        private static readonly Dictionary<string,int> _lastIndex = new();

        static Dialogues()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "virgil-dialogues.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var node = JsonNode.Parse(json) as JsonObject;
                    _root = node ?? new JsonObject();
                }
                else
                {
                    _root = new JsonObject();
                }
            }
            catch
            {
                _root = new JsonObject();
            }
        }

        private static string Pick(string key, string fallback = "…")
        {
            try
            {
                if (!_root.TryGetPropertyValue(key, out var node) || node is not JsonArray arr || arr.Count == 0)
                    return fallback;

                // liste de strings valides
                var items = arr.Select(x => x?.ToString())
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .ToList();
                if (items.Count == 0) return fallback;

                // éviter de répéter l'élément précédent
                int lastIdx = _lastIndex.ContainsKey(key) ? _lastIndex[key] : -1;

                // candidates: tous sauf le dernier utilisé (si possible)
                var candidates = items.Select((v, i) => (v, i)).ToList();
                if (lastIdx >= 0 && lastIdx < items.Count && items.Count > 1)
                    candidates = candidates.Where(t => t.i != lastIdx).ToList();

                var rnd = new Random();
                var pick = candidates[rnd.Next(candidates.Count)];
                _lastIndex[key] = pick.i;
                return pick.v ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static string Startup() => Pick("startup", "Démarrage…");
        public static string SurveillanceStart() => Pick("surveillance_start", "Surveillance activée.");
        public static string SurveillanceStop() => Pick("surveillance_stop", "Surveillance arrêtée.");

        public static string PulseLineByTimeOfDay()
        {
            var h = DateTime.Now.Hour;
            if (h >= 6 && h < 12) return Pick("time_morning", "Bonjour !");
            if (h >= 12 && h < 18) return Pick("time_afternoon", "On continue.");
            if (h >= 18 && h < 23) return Pick("time_evening", "Bonne soirée.");
            return Pick("time_night", "Douce nuit.");
        }

        public static string AlertTemp() => Pick("alert_temp", "Attention à la température.");

        public static string Action(string key) => Pick($"action_{key}", "");
    }
}
