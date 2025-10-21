#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.Core;

namespace Virgil.App.Controls
{
    /// <summary>
    /// ViewModel for the Virgil avatar. Exposes the current mood color and message to the UI.
    /// </summary>
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private readonly MoodService _moodService;
        private readonly DialogService _dialogService;
        private string _message = string.Empty;

        public VirgilAvatarViewModel(MoodService moodService, DialogService dialogService)
        {
            _moodService = moodService;
            _dialogService = dialogService;
            _moodService.MoodChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(MoodColor));
            };
        }

        /// <summary>
        /// Gets the hexadecimal colour associated with the current mood.
        /// </summary>
        public string MoodColor => _moodService.GetMoodColor();

        /// <summary>
        /// Gets or sets the current avatar message.
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Sets the mood and picks a random dialogue from the specified category.
        /// </summary>
        public void SetMood(Mood mood, string dialogCategory)
        {
            _moodService.CurrentMood = mood;
            Message = _dialogService.GetRandomMessage(dialogCategory);
            OnPropertyChanged(nameof(MoodColor));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
