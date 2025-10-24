#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace Virgil.App.Controls
{
    public sealed class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private Brush _bodyBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x7C, 0x4E));
        public Brush BodyBrush { get => _bodyBrush; set { _bodyBrush = value; OnChanged(); } }

        private Brush _eyeBrush = Brushes.White;
        public Brush EyeBrush { get => _eyeBrush; set { _eyeBrush = value; OnChanged(); } }

        public double EyeWidth { get; set; } = 26;
        public double EyeHeight { get; set; } = 26;
        public double EyeSeparation { get; set; } = 16;

        private double _blink = 1.0;
        public double Blink { get => _blink; set { _blink = value; OnChanged(); } }

        public bool IsFlatEyes { get; private set; } = true;
        public bool IsAngry { get; private set; }
        public bool IsLove { get; private set; }
        public bool IsSad { get; private set; }
        public bool IsCat { get; private set; }
        public bool IsDevil { get; private set; }

        private void Flags(bool flat, bool angry, bool love, bool sad, bool cat, bool devil)
        {
            IsFlatEyes = flat; OnChanged(nameof(IsFlatEyes));
            IsAngry = angry; OnChanged(nameof(IsAngry));
            IsLove = love; OnChanged(nameof(IsLove));
            IsSad = sad; OnChanged(nameof(IsSad));
            IsCat = cat; OnChanged(nameof(IsCat));
            IsDevil = devil; OnChanged(nameof(IsDevil));
        }

        public void SetMood(string mood)
        {
            mood = (mood ?? "neutral").ToLowerInvariant();

            BodyBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x7C, 0x4E));
            EyeBrush = Brushes.White;

            switch (mood)
            {
                case "neutral":
                case "vigilant":
                case "proud":
                    Flags(true, false, false, false, false, false);
                    break;
                case "alert":
                case "angry":
                    Flags(false, true, false, false, false, false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x3C, 0x3C));
                    break;
                case "love":
                    Flags(false, false, true, false, false, false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x6A, 0xB3));
                    break;
                case "sad":
                    Flags(false, false, false, true, false, false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x66, 0x7A));
                    break;
                case "cat":
                    Flags(true, false, false, false, true, false);
                    break;
                case "devil":
                    Flags(false, true, false, false, false, true);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x3C, 0x3C));
                    break;
                default:
                    Flags(true, false, false, false, false, false);
                    break;
            }
        }

        private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
        private int _phase;
        private int _wait;
        private readonly Random _rng = new();

        public VirgilAvatarViewModel()
        {
            Next();
            _blinkTimer.Tick += (_, __) => Step();
            _blinkTimer.Start();
        }

        private void Next() => _wait = _rng.Next(18, 36);

        private void Step()
        {
            if (_wait > 0) { _wait--; return; }
            switch (_phase)
            {
                case 0: Blink = 0.6; _phase = 1; break;
                case 1: Blink = 0.1; _phase = 2; break;
                case 2: Blink = 0.5; _phase = 3; break;
                default:
                    Blink = 1.0; _phase = 0; Next(); break;
            }
        }
    }
}
