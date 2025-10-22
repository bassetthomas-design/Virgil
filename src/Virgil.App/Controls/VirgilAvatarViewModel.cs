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

        // 0..100 mapped to ~1.00..1.12 for a gentle pulse driven by progress
        private double _progressScale = 1.0;
        public double ProgressScale
        {
            get => _progressScale;
            private set { _progressScale = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetMood(string moodKey, string context = "")
        {
            var rgb = _moods.ResolveColor(moodKey);
            CoreBrush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));

            var key = NormalizeKey(context);
            if (_lines.TryGetValue(key, out var arr) && arr.Length > 0)
                CurrentLine = arr[_rng.Next(arr.Length)];
            else
                CurrentLine = _moods.DefaultLine(moodKey);
        }

        public void SetProgress(double percent)
        {
            if (percent < 0) percent = 0; if (percent > 100) percent = 100;
            // Ease-out mapping to 1.00 .. 1.12
            var t = percent / 100.0;
            var eased = 1.0 + 0.12 * (1 - Math.Pow(1 - t, 2)); // quad ease-out
            ProgressScale = eased;
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
            return new Dictionary<string, string[]>
            {
                ["general"] = new[] { "Tout roule.", "Toujours là ✨" }
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
