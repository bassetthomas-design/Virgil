// AUTO-PATCH: disambiguate Mood by using Virgil.Domain
using System;
using System.Windows.Media;
using Virgil.Domain; // single source of truth for Mood

namespace Virgil.App.Services
{
    public class PulseController
    {
        private readonly Brush _okBrush = Brushes.MediumSpringGreen;
        private readonly Brush _warnBrush = Brushes.Gold;
        private readonly Brush _badBrush = Brushes.OrangeRed;

        public Brush GetBrushForMood(Mood mood)
        {
            switch (mood)
            {
                case Mood.Calm: return _okBrush;
                case Mood.Focus: return _okBrush;
                case Mood.Neutral: return _warnBrush;
                case Mood.Tense: return _warnBrush;
                case Mood.Stressed: return _badBrush;
                default: return _warnBrush;
            }
        }
    }
}
