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
        private void OnChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // Couleurs & brosses
        private Brush _bodyBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x7C, 0x4E)); // vert
        public Brush BodyBrush { get => _bodyBrush; set { _bodyBrush = value; OnChanged(); } }

        private Brush _eyeBrush = Brushes.White;
        public Brush EyeBrush { get => _eyeBrush; set { _eyeBrush = value; OnChanged(); } }

        // Yeux (géométrie simple)
        private double _eyeWidth = 26;
        public double EyeWidth  { get => _eyeWidth; set { _eyeWidth = value; OnChanged(); } }

        private double _eyeHeight = 26;
        public double EyeHeight { get => _eyeHeight; set { _eyeHeight = value; OnChanged(); } }

        private double _eyeSeparation = 16;
        public double EyeSeparation { get => _eyeSeparation; set { _eyeSeparation = value; OnChanged(); } }

        // clignement (0..1) = scaleY
        private double _blink = 1.0;
        public double Blink { get => _blink; set { _blink = value; OnChanged(); } }

        // états d’humeur
        public bool IsFlatEyes { get; private set; } = true;
        public bool IsAngry    { get; private set; }
        public bool IsLove     { get; private set; }
        public bool IsSad      { get; private set; }
        public bool IsCat      { get; private set; }
        public bool IsDevil    { get; private set; }

        private void SetFlags(bool flat, bool angry, bool love, bool sad, bool cat, bool devil)
        {
            IsFlatEyes = flat; OnChanged(nameof(IsFlatEyes));
            IsAngry    = angry; OnChanged(nameof(IsAngry));
            IsLove     = love; OnChanged(nameof(IsLove));
            IsSad      = sad; OnChanged(nameof(IsSad));
            IsCat      = cat; OnChanged(nameof(IsCat));
            IsDevil    = devil; OnChanged(nameof(IsDevil));
        }

        public void SetMood(string mood)
        {
            mood = (mood ?? "neutral").ToLowerInvariant();

            // valeurs par défaut
            BodyBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x7C, 0x4E)); // vert
            EyeBrush  = Brushes.White;
            EyeWidth = EyeHeight = 26;
            EyeSeparation = 16;

            switch (mood)
            {
                case "neutral":
                case "vigilant":
                case "proud":
                    SetFlags(flat: true, angry:false, love:false, sad:false, cat:false, devil:false);
                    break;

                case "alert":
                case "angry":
                    SetFlags(flat:false, angry:true, love:false, sad:false, cat:false, devil:false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x3C, 0x3C)); // rouge
                    break;

                case "love":
                    SetFlags(flat:false, angry:false, love:true, sad:false, cat:false, devil:false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x6A, 0xB3));
                    break;

                case "sad":
                    SetFlags(flat:false, angry:false, love:false, sad:true, cat:false, devil:false);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x66, 0x7A));
                    break;

                case "cat":
                    SetFlags(flat:true, angry:false, love:false, sad:false, cat:true, devil:false);
                    break;

                case "devil":
                    SetFlags(flat:false, angry:true, love:false, sad:false, cat:false, devil:true);
                    BodyBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x3C, 0x3C));
                    break;

                default:
                    SetFlags(flat:true, angry:false, love:false, sad:false, cat:false, devil:false);
                    break;
            }
        }

        // --- animation : clignement automatique
        private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
        private int _blinkPhase = 0;
        private int _ticksToNextBlink = 0;
        private readonly Random _rng = new();

        public VirgilAvatarViewModel()
        {
            PlanNextBlink();
            _blinkTimer.Tick += (_, __) => StepBlink();
            _blinkTimer.Start();
        }

        private void PlanNextBlink()
        {
            _ticksToNextBlink = _rng.Next(18, 36); // ~2–4s (120ms * n)
        }

        private void StepBlink()
        {
            if (_ticksToNextBlink > 0)
            {
                _ticksToNextBlink--;
                return;
            }

            // phases: 0->1->2->3->open
            switch (_blinkPhase)
            {
                case 0: Blink = 0.5; _blinkPhase = 1; break;
                case 1: Blink = 0.1; _blinkPhase = 2; break;
                case 2: Blink = 0.5; _blinkPhase = 3; break;
                default:
                    Blink = 1.0; _blinkPhase = 0; PlanNextBlink(); break;
            }
        }
    }
}
