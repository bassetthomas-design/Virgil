using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Virgil.App.Models;
using Virgil.Core;
using Virgil.Domain;
using MonitoringService = Virgil.App.Services.MonitoringService;
using Mood = Virgil.Domain.Mood;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel de monitoring qui expose les métriques système remontées par MonitoringService.
    /// Sert à la fois pour l'affichage (dashboard) et pour piloter l'humeur de Virgil.
    /// </summary>
    public class MonitoringViewModel : INotifyPropertyChanged
    {
        private readonly ISystemMonitorService? _systemMonitoring = null!;
        private readonly MonitoringService? _legacyMonitoring = null!;
        private readonly SettingsService? _settings = null!;
        private readonly NetworkInsightService? _network = null!;
        private readonly SynchronizationContext? _uiContext;

        public MonitoringViewModel(ISystemMonitorService monitoring)
        {
            _systemMonitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _uiContext = SynchronizationContext.Current;

            _systemMonitoring.SnapshotUpdated += OnSystemMetricsUpdated;
            AvatarSource = AvatarService.GetAvatarPath(_currentMood);
        }

        public MonitoringViewModel(
            ISystemMonitorService monitoring,
            SettingsService settings,
            NetworkInsightService network)
            : this(monitoring)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public MonitoringViewModel(
            MonitoringService monitoring,
            SettingsService settings,
            NetworkInsightService network)
        {
            _legacyMonitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _uiContext = SynchronizationContext.Current;

            _legacyMonitoring.Updated += OnLegacyMetricsUpdated;
            _legacyMonitoring.Start();
            AvatarSource = AvatarService.GetAvatarPath(_currentMood);
        }

        // Ctor sans paramètres pour le design-time ou certains usages XAML / legacy.
        public MonitoringViewModel()
            : this(new MonitoringService(), new SettingsService(), new NetworkInsightService())
        {
        }

        private Mood _currentMood = Mood.Neutral;
        public Mood CurrentMood
        {
            get => _currentMood;
            set
            {
                if (Equals(_currentMood, value)) return;
                _currentMood = value;
                OnPropertyChanged();
                AvatarSource = AvatarService.GetAvatarPath(value);
            }
        }

        private string _avatarSource = string.Empty;

        public string AvatarSource
        {
            get => _avatarSource;
            private set
            {
                if (_avatarSource == value) return;
                _avatarSource = value;
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

        private void OnSystemMetricsUpdated(object? sender, SystemMonitorSnapshot snapshot)
            => DispatchMetrics(() => ApplySnapshot(snapshot));

        private void OnLegacyMetricsUpdated(object? sender, MetricsEventArgs e)
            => DispatchMetrics(() => ApplySnapshot(e));

        private void DispatchMetrics(Action apply)
        {
            // Garantit que les notifications PropertyChanged partent du thread UI.
            if (_uiContext is { } ctx)
            {
                ctx.Post(_ => apply(), null);
            }
            else
            {
                apply();
            }
        }

        private void ApplySnapshot(SystemMonitorSnapshot snapshot)
        {
            CpuUsage = snapshot.CpuUsage;
            GpuUsage = snapshot.GpuUsage;
            RamUsage = snapshot.RamUsage;
            DiskUsage = snapshot.DiskUsage;
            CpuTemp = snapshot.CpuTemperature;
            GpuTemp = snapshot.GpuTemperature;
            DiskTemp = snapshot.DiskTemperature;

            UpdateMood(snapshot.CpuUsage, snapshot.GpuUsage, snapshot.RamUsage, snapshot.DiskUsage,
                snapshot.CpuTemperature, snapshot.GpuTemperature, snapshot.DiskTemperature);
        }

        private void ApplySnapshot(MetricsEventArgs metrics)
        {
            CpuUsage = metrics.CpuUsage;
            GpuUsage = metrics.GpuUsage;
            RamUsage = metrics.RamUsage;
            DiskUsage = metrics.DiskUsage;
            CpuTemp = metrics.CpuTemp;
            GpuTemp = metrics.GpuTemp;
            DiskTemp = metrics.DiskTemp;

            UpdateMood(metrics.CpuUsage, metrics.GpuUsage, metrics.RamUsage, metrics.DiskUsage,
                metrics.CpuTemp, metrics.GpuTemp, metrics.DiskTemp);
        }

        private void UpdateMood(double cpu, double gpu, double ram, double disk, double cpuTemp, double gpuTemp, double diskTemp)
        {
            var stats = new SystemStats(cpu, gpu, ram, disk, cpuTemp, gpuTemp, diskTemp);
            CurrentMood = MoodEngine.FromStats(stats);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
