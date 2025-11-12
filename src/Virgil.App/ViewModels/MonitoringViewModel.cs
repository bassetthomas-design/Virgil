using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public partial class MonitoringViewModel : INotifyPropertyChanged
    {
        public MonitoringViewModel() { }
        // Shim to match existing MainWindow usage (temporary)
        public MonitoringViewModel(object a, object b, object c) : this() { }

        private Mood _currentMood = default;
        public Mood CurrentMood
        {
            get => _currentMood;
            set { if (Equals(_currentMood, value)) return; _currentMood = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
