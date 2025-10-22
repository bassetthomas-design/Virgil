#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Virgil.App.Controls
{
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private string _message = "Prêt.";
        private System.Windows.Media.Brush _glowBrush =
            new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4B, 0x9C, 0xFF)); // bleu neutre
        private double _progress = 0.0;
        private bool _isIndeterminate;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Message
        {
            get => _message;
            private set { _message = value; OnPropertyChanged(); }
        }

        public System.Windows.Media.Brush GlowBrush
        {
            get => _glowBrush;
            private set { _glowBrush = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            private set { _progress = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        // --- API appelée par MainWindow ---

        public void SetMessage(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Message = message!;
        }

        public void SetMood(string mood)
        {
            switch ((mood ?? "neutral").ToLowerInvariant())
            {
                case "proud":
                    GlowBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x3C, 0xC1, 0x4B)); // vert
                    break;
                case "vigilant":
                    GlowBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xA6, 0x2B)); // orange
                    break;
                case "alert":
                    GlowBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0x4B, 0x4B)); // rouge
                    break;
                case "resting":
                    GlowBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x6E, 0xD3, 0xFF)); // cyan doux
                    break;
                default:
                    GlowBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x4B, 0x9C, 0xFF)); // neutre
                    break;
            }
        }

        public void SetProgress(double percent, bool indeterminate)
        {
            IsIndeterminate = indeterminate;
            Progress = percent;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}