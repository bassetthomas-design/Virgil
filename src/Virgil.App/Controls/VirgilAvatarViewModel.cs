#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using Virgil.Core.Services;

namespace Virgil.App.Controls
{
    /// <summary>
    /// VM très léger : gère couleur (humeur) + petite ligne de texte.
    /// </summary>
    public sealed class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private readonly MoodService _moods = new();
        private readonly Dictionary<string, string[]> _lines = LoadDialogues();
        private readonly Random _rng = new();

        private string _currentLine = "Salut !";
        public string CurrentLine { get => _currentLine; private set { _currentLine = value; OnPropertyChanged(); } }

        private SolidColorBrush _coreBrush = new(Color.FromRgb(0x40, 0xA0, 0xFF));
        public SolidColorBrush CoreBrush { get => _coreBrush; private set { _coreBrush = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetMood(string moodKey, string context = "")
        {
            var color = _moods.ResolveColor(moodKey);
            CoreBrush = new SolidColorBrush(color);

            // Ligne contextuelle si dispo
            var key = NormalizeKey(context);
            if (_lines.TryGetValue(key, out var arr) && arr.Length > 0)
                CurrentLine = arr[_rng.Next(arr.Length)];
            else
                CurrentLine = _moods.DefaultLine(moodKey);
        }

        private static string NormalizeKey(string s)
            => string.IsNullOrWhiteSpace(s) ? "General" : s.Trim().ToLowerInvariant();

        private static Dictionary<string, string[]> LoadDialogues()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                // Le fichier est copié dans la sortie par le .csproj du Core (CopyToOutputDirectory)
                var path = Path.Combine(baseDir, "virgil-dialogues.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
                    return dict ?? new();
                }
            }
            catch { }
            // fallback minimal
            return new Dictionary<string, string[]>
            {
                ["general"] = new[]
                {
                    "Tout roule.",
                    "Toujours là ✨",
                    "On surveille en silence.",
                },
                ["startup"] = new[]
                {
                    "Rebonjour 👋",
                    "Virgil en place.",
                },
                ["full maintenance"] = new[]
                {
                    "Ça nettoie, ça met à jour…",
                    "Opération grand ménage 🧹",
                }
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
