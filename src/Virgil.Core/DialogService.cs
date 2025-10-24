using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Virgil.Core
{
    /// <summary>
    /// Provides contextual dialogue sentences for the Virgil avatar. The dialogues
    /// are loaded from a JSON file that maps categories to lists of phrases.
    /// </summary>
    public class DialogService
    {
        private Dictionary<string, List<string>> _dialogues = new();

        public DialogService()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "virgil-dialogues.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (data != null)
                    {
                        _dialogues = data;
                    }
                }
            }
            catch
            {
                // If the file can't be loaded, leave the dictionary empty.
            }
        }

        /// <summary>
        /// Returns a random phrase from the specified category. If the category
        /// does not exist or contains no phrases, an empty string is returned.
        /// </summary>
        public string GetRandomMessage(string category)
        {
            if (_dialogues.TryGetValue(category, out var phrases) && phrases.Count > 0)
            {
                var index = Random.Shared.Next(phrases.Count);
                return phrases[index];
            }
            return string.Empty;
        }
    }
}