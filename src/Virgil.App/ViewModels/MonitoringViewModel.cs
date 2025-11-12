using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public partial class MonitoringViewModel : INotifyPropertyChanged
    {
        private Mood _currentMood = default(Mood);
        public Mood CurrentMood
        {
            get => _currentMood;
            set { if (_currentMood.Equals(value)) return; _currentMood = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
