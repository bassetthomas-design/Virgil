using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _svc;

        public SettingsViewModel(SettingsService svc)
        {
            _svc = svc;

            // Charger une "copie" (en champs) pour permettre Annuler sans effet de bord
            var s = _svc.Settings;

            _monitoringIntervalMs = s.MonitoringIntervalMs;
            _defaultMessageTtlMs = s.DefaultMessageTtlMs;
            _companionTalkative = s.CompanionTalkative;

            _warnTemp = s.Mood.WarnTemp;
            _alertTemp = s.Mood.AlertTemp;
            _warnCpu = s.Mood.WarnCpu;
        }

        private int _monitoringIntervalMs;
        public int MonitoringIntervalMs
        {
            get => _monitoringIntervalMs;
            set { _monitoringIntervalMs = value; OnPropertyChanged(); }
        }

        private int _defaultMessageTtlMs;
        public int DefaultMessageTtlMs
        {
            get => _defaultMessageTtlMs;
            set { _defaultMessageTtlMs = value; OnPropertyChanged(); }
        }

        private bool _companionTalkative;
        public bool CompanionTalkative
        {
            get => _companionTalkative;
            set { _companionTalkative = value; OnPropertyChanged(); }
        }

        private double _warnTemp;
        public double WarnTemp
        {
            get => _warnTemp;
            set { _warnTemp = value; OnPropertyChanged(); }
        }

        private double _alertTemp;
        public double AlertTemp
        {
            get => _alertTemp;
            set { _alertTemp = value; OnPropertyChanged(); }
        }

        private double _warnCpu;
        public double WarnCpu
        {
            get => _warnCpu;
            set { _warnCpu = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Applique les valeurs au SettingsService et persiste.
        /// </summary>
        public void Save()
        {
            var s = _svc.Settings;

            s.MonitoringIntervalMs = _monitoringIntervalMs;
            s.DefaultMessageTtlMs = _defaultMessageTtlMs;
            s.CompanionTalkative = _companionTalkative;

            s.Mood.WarnTemp = _warnTemp;
            s.Mood.AlertTemp = _alertTemp;
            s.Mood.WarnCpu = _warnCpu;

            _svc.Save();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
