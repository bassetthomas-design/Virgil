using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Virgil.Domain
{
    /// <summary>
    /// Loads Virgil's phrases from the VirgilPhrases.json asset and exposes
    /// helpers to pick random phrases by category. This is the foundation for
    /// the narration system (startup lines, ambient chatter, THPS2 refs, etc.).
    /// </summary>
    public class VirgilPhraseService
    {
        private readonly IReadOnlyList<VirgilPhrase> _phrases;
        private readonly Random _random = new();

        public VirgilPhraseService(string? baseDirectory = null)
        {
            baseDirectory ??= AppContext.BaseDirectory;

            // Try a few reasonable locations for the JSON file depending on
            // how the application is deployed or where the assets are copied.
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, "Assets", "VirgilPhrases.json"),
                Path.Combine(baseDirectory, "Virgil.Domain", "Assets", "VirgilPhrases.json"),
            };

            string? jsonPath = null;
            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    jsonPath = path;
                    break;
                }
            }

            if (jsonPath is null)
            {
                _phrases = Array.Empty<VirgilPhrase>();
                return;
            }

            var json = File.ReadAllText(jsonPath);
            _phrases = JsonSerializer.Deserialize<List<VirgilPhrase>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<VirgilPhrase>();
        }

        /// <summary>
        /// All phrases currently loaded from the JSON library.
        /// </summary>
        public IReadOnlyList<VirgilPhrase> Phrases => _phrases;

        /// <summary>
        /// Picks a random phrase, optionally filtered by category.
        /// </summary>
        public VirgilPhrase? GetRandom(string? category = null)
        {
            IEnumerable<VirgilPhrase> pool = _phrases;

            if (!string.IsNullOrWhiteSpace(category))
            {
                pool = pool.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            var list = pool.ToList();
            if (list.Count == 0)
            {
                return null;
            }

            return list[_random.Next(list.Count)];
        }

        public VirgilPhrase? GetRandomAmbient() => GetRandom("ambient");
        public VirgilPhrase? GetRandomStartup() => GetRandom("startup");
        public VirgilPhrase? GetRandomThps2() => GetRandom("thps2");
    }
}
