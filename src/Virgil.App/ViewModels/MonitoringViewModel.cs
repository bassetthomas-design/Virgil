using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Core;
using Virgil.App.Models;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel de monitoring qui expose les métriques système remontées par MonitoringService.
    /// Sert à la fois pour l'affichage (dashboard) et pour piloter l'humeur de Virgil.
    /// </summary>
    public class MonitoringViewModel : INotifyPropertyChanged
    {
        private readonly MonitoringService _monitoring;
        private readonly SettingsService _settings;
        private readonly NetworkInsightService _network;

        public MonitoringViewModel(
            MonitoringService monitoring,
            SettingsService settings,
            NetworkInsightService network)
        {
            _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _network = network ?? throw new ArgumentNullException(nameof(network));

            _monitoring.Updated += OnMetricsUpdated;
            _monitoring.Start();
        }

        // Ctor sans paramètres pour le design-time ou certains usages XAML / legacy.
        public MonitoringViewModel()
            : this(new MonitoringService(), new SettingsService(), new NetworkInsightService())
        {
        }

        private Mood _currentMood;
        public Mood CurrentMood
        {
            get => _currentMood;
            set
            {
                if (Equals(_currentMood, value)) return;
                _currentMood = value;
                OnPropertyChanged();
            }
        }

        private double _cpuUsage;
        public double CpuUsage
        {
            get => _cpuUsage;
            private set
            {
                if (Math.Abs(_cpuUsage - value) < 0.1) return;
                _cpuUsage = value;
                OnPropertyChanged();
            }
        }

        private double _gpuUsage;
        public double GpuUsage
        {
            get => _gpuUsage;
            private set
            {
                if (Math.Abs(_gpuUsage - value) < 0.1) return;
                _gpuUsage = value;
                OnPropertyChanged();
            }
        }

        private double _ramUsage;
        public double RamUsage
        {
            get => _ramUsage;
            private set
            {
                if (Math.Abs(_ramUsage - value) < 0.1) return;
                _ramUsage = value;
                OnPropertyChanged();
            }
        }

        private double _diskUsage;
        public double DiskUsage
        {
            get => _diskUsage;
            private set
            {
                if (Math.Abs(_diskUsage - value) < 0.1) return;
                _diskUsage = value;
                OnPropertyChanged();
            }
        }

        private double _cpuTemp;
        public double CpuTemp
        {
            get => _cpuTemp;
            private set
            {
                if (Math.Abs(_cpuTemp - value) < 0.1) return;
                _cpuTemp = value;
                OnPropertyChanged();
            }
        }

        private double _gpuTemp;
        public double GpuTemp
        {
            get => _gpuTemp;
            private set
            {
                if (Math.Abs(_gpuTemp - value) < 0.1) return;
                _gpuTemp = value;
                OnPropertyChanged();
            }
        }

        private double _diskTemp;
        public double DiskTemp
        {
            get => _diskTemp;
            private set
            {
                if (Math.Abs(_diskTemp - value) < 0.1) return;
                _diskTemp = value;
                OnPropertyChanged();
            }
        }

        private void OnMetricsUpdated(object? sender, MetricsEventArgs e)
        {
            CpuUsage = e.CpuUsage;
            GpuUsage = e.GpuUsage;
            RamUsage = e.RamUsage;
            DiskUsage = e.DiskUsage;
            CpuTemp = e.CpuTemp;
            GpuTemp = e.GpuTemp;
            DiskTemp = e.DiskTemp;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
