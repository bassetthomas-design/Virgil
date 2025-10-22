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
    /// VM : gère la phrase courante et la couleur selon l’humeur.
    /// </summary>
    public sealed class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private readonly MoodService _moods = new();
        private readonly Dictionary<string, string[]> _lines = LoadDialogues();
        private readonly Random _rng = new();

        private string _currentLine = "Salut !";
        public string CurrentLine
        {
            get => _currentLine;
            private set { _currentLine = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _coreBrush = new(Color.FromRgb(0x40, 0xA0, 0xFF));
        public SolidColorBrush CoreBrush
        {
            get => _coreBrush;
            private set { _coreBrush = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetMood(string moodKey, string context = "")
        {
            // Resolve RGB from Core (no WPF there), convert to WPF Color here
            var rgb = _moods.ResolveColor(moodKey);
            CoreBrush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));

            // Pick a contextual line
            var key = NormalizeKey(context);
            if (_lines.TryGetValue(key, out var arr) && arr.Length > 0)
                CurrentLine = arr[_rng.Next(arr.Length)];
            else
                CurrentLine = _moods.DefaultLine(moodKey);
        }

        private static string NormalizeKey(string s)
            => string.IsNullOrWhiteSpace(s) ? "general" : s.Trim().ToLowerInvariant();

        private static Dictionary<string, string[]> LoadDialogues()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var path = Path.Combine(baseDir, "virgil-dialogues.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
                    return dict ?? new();
                }
            }
            catch { /* ignore */ }

            // Fallback minimal si le JSON n'est pas trouvé
            return new Dictionary<string, string[]>
            {
                ["general"] = new[]
                {
                    "Tout roule.",
                    "Toujours là ✨",
                    "On surveille en silence."
                }
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
