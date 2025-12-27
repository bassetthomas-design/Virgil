using System;
using System.Threading.Tasks;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        private int _progressPercent;
        private string? _progressText;

        public Task Say(string text) => Task.CompletedTask;
        public Task Say(string text, MessageType type, bool pinned = false, int? ttlMs = null) => Task.CompletedTask;

        public int ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (clamped == _progressPercent)
                {
                    return;
                }

                _progressPercent = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProgressVisible));
            }
        }

        public string? ProgressText
        {
            get => _progressText;
            private set
            {
                if (_progressText == value)
                {
                    return;
                }

                _progressText = value;
                OnPropertyChanged();
            }
        }

        public bool IsProgressVisible => _progressPercent > 0 && _progressPercent < 100;

        public void Progress(int percent, string? text = null)
        {
            ProgressPercent = percent;
            if (!string.IsNullOrWhiteSpace(text))
            {
                ProgressText = text;
            }
        }
        public void Progress(string text, int percent) => Progress(percent, text);

    }
}
