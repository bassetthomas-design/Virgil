using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Models;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _svc;
        public SettingsViewModel(SettingsService svc) { _svc = svc; _s = svc.Settings; }

        private AppSettings _s;
        public int MonitoringIntervalMs { get => _s.MonitoringIntervalMs; set { _s.MonitoringIntervalMs = value; OnPropertyChanged(); } }
        public int DefaultMessageTtlMs { get => _s.DefaultMessageTtlMs; set { _s.DefaultMessageTtlMs = value; OnPropertyChanged(); } }
        public bool CompanionTalkative { get => _s.CompanionTalkative; set { _s.CompanionTalkative = value; OnPropertyChanged(); } }
        public double WarnTemp { get => _s.Mood.WarnTemp; set { _s.Mood.WarnTemp = value; OnPropertyChanged(); } }
        public double AlertTemp { get => _s.Mood.AlertTemp; set { _s.Mood.AlertTemp = value; OnPropertyChanged(); } }
        public double WarnCpu { get => _s.Mood.WarnCpu; set { _s.Mood.WarnCpu = value; OnPropertyChanged(); } }

        public void Save() => _svc.Save();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
