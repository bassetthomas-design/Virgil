#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media; // WPF

namespace Virgil.App.Controls
{
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private string _message = "Prêt.";
        private Brush _glowBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x9C, 0xFF)); // bleu doux
        private double _progress = 0.0;
        private bool _isIndeterminate;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Message
        {
            get => _message;
            private set { _message = value; OnPropertyChanged(); }
        }

        public Brush GlowBrush
        {
            get => _glowBrush;
            private set { _glowBrush = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            private set { _progress = value; OnPropertyChanged(); }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        // --- API simple appelée par MainWindow ---

        public void SetMessage(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Message = message!;
        }

        public void SetMood(string mood)
        {
            // mapping très simple -> couleur
            switch ((mood ?? "neutral").ToLowerInvariant())
            {
                case "proud":      GlowBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0xC1, 0x4B)); break; // vert
                case "vigilant":   GlowBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA6, 0x2B)); break; // orange
                case "alert":      GlowBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x4B, 0x4B)); break; // rouge
                case "resting":    GlowBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0xD3, 0xFF)); break; // cyan doux
                default:           GlowBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x9C, 0xFF)); break; // neutre
            }
        }

        public void SetProgress(double percent, bool indeterminate)
        {
            IsIndeterminate = indeterminate;
            Progress = Math.Max(0, Math.Min(100, percent));
        }

        // utilitaire INotifyPropertyChanged
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
