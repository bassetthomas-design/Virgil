using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Virgil.App.Controls;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.App.Utils;
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
        private readonly DispatcherTimer? _cooldownTimer;

        public MonitoringViewModel(ISystemMonitorService monitoring)
        {
            _systemMonitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _uiContext = SynchronizationContext.Current;

            _systemMonitoring.SnapshotUpdated += OnSystemMetricsUpdated;
            AvatarSource = AvatarService.GetAvatarPath(_currentMood);
            _cooldownTimer = CreateCooldownTimer();
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
            _cooldownTimer = CreateCooldownTimer();
        }

        // Ctor sans paramètres pour le design-time ou certains usages XAML / legacy.
        public MonitoringViewModel()
            : this(new MonitoringService(), new SettingsService(), new NetworkInsightService())
        {
        }

        public AvatarTelemetryAdapter AvatarTelemetry { get; } = new();

        private DateTime? _lastUpdateTimeUtc;
        public DateTime? LastUpdateTimeUtc
        {
            get => _lastUpdateTimeUtc;
            private set
            {
                if (_lastUpdateTimeUtc == value) return;
                _lastUpdateTimeUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastTelemetryUpdateUtc));
                UpdateTelemetrySchedule(_nextTelemetryUpdateUtc);
            }
        }

        public DateTime LastTelemetryUpdateUtc => LastUpdateTimeUtc ?? DateTime.MinValue;

        private DateTime? _nextTelemetryUpdateUtc;
        public DateTime NextTelemetryUpdateUtc => _nextTelemetryUpdateUtc ?? DateTime.MinValue;

        private TimeSpan _telemetryCooldownRemaining = TimeSpan.Zero;
        public TimeSpan TelemetryCooldownRemaining
        {
            get => _telemetryCooldownRemaining;
            private set
            {
                if (_telemetryCooldownRemaining == value) return;
                _telemetryCooldownRemaining = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TelemetryCooldownRemainingText));
                OnPropertyChanged(nameof(TelemetryCooldownDisplay));
            }
        }

        private TimeSpan? _telemetryCooldownDuration;

        private string _telemetryCooldownState = TelemetryCooldownStates.Unknown;
        public string TelemetryCooldownState
        {
            get => _telemetryCooldownState;
            private set
            {
                if (_telemetryCooldownState == value) return;
                _telemetryCooldownState = value;
                OnPropertyChanged();
            }
        }

        public string TelemetryCooldownRemainingText
            => _nextTelemetryUpdateUtc.HasValue
                ? FormatCooldown(TelemetryCooldownRemaining)
                : "--:--";

        public string TelemetryCooldownDisplay
            => $"🔄 Prochaine mise à jour dans {TelemetryCooldownRemainingText}";

        private TimeSpan? _dataAge;
        public TimeSpan? DataAge
        {
            get => _dataAge;
            private set
            {
                if (_dataAge == value) return;
                _dataAge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataAgeSeconds));
            }
        }

        public double? DataAgeSeconds => DataAge?.TotalSeconds;

#if DEBUG
        private bool _useDebugStress;
        public bool UseDebugStress
        {
            get => _useDebugStress;
            set
            {
                if (value == _useDebugStress) return;
                _useDebugStress = value;
                OnPropertyChanged();

                if (_useDebugStress)
                {
                    AvatarTelemetry.SetStress(_debugStress);
                }
            }
        }

        private double _debugStress = 0.25;
        public double DebugStress
        {
            get => _debugStress;
            set
            {
                var clamped = Math.Clamp(value, 0d, 1d);
                if (Math.Abs(_debugStress - clamped) < 0.0001) return;
                _debugStress = clamped;
                OnPropertyChanged();

                if (UseDebugStress)
                {
                    AvatarTelemetry.SetStress(_debugStress);
                }
            }
        }
#endif

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
                OnPropertyChanged(nameof(CpuUsageText));
            }
        }

        private bool _cpuUsageIsStale;
        public bool CpuUsageIsStale
        {
            get => _cpuUsageIsStale;
            private set
            {
                if (_cpuUsageIsStale == value) return;
                _cpuUsageIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _cpuUsageLastUpdatedUtc;
        public DateTime? CpuUsageLastUpdatedUtc
        {
            get => _cpuUsageLastUpdatedUtc;
            private set
            {
                if (_cpuUsageLastUpdatedUtc == value) return;
                _cpuUsageLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuUsageText));
            }
        }

        public string CpuUsageText => FormatMetric(CpuUsage, CpuUsageLastUpdatedUtc, "%");

        private double _gpuUsage;
        public double GpuUsage
        {
            get => _gpuUsage;
            private set
            {
                if (Math.Abs(_gpuUsage - value) < 0.1) return;
                _gpuUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuUsageText));
            }
        }

        private bool _gpuUsageIsStale;
        public bool GpuUsageIsStale
        {
            get => _gpuUsageIsStale;
            private set
            {
                if (_gpuUsageIsStale == value) return;
                _gpuUsageIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _gpuUsageLastUpdatedUtc;
        public DateTime? GpuUsageLastUpdatedUtc
        {
            get => _gpuUsageLastUpdatedUtc;
            private set
            {
                if (_gpuUsageLastUpdatedUtc == value) return;
                _gpuUsageLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuUsageText));
            }
        }

        public string GpuUsageText => FormatMetric(GpuUsage, GpuUsageLastUpdatedUtc, "%");

        private string _activeGpuName = "Unknown";
        public string ActiveGpuName
        {
            get => _activeGpuName;
            private set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
                if (string.Equals(_activeGpuName, normalized, StringComparison.Ordinal)) return;
                _activeGpuName = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuHeaderText));
                OnPropertyChanged(nameof(GpuUsageDisplayText));
            }
        }

        private double _activeGpuPercent;
        public double ActiveGpuPercent
        {
            get => _activeGpuPercent;
            private set
            {
                if (Math.Abs(_activeGpuPercent - value) < 0.1) return;
                _activeGpuPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuUsageDisplayText));
            }
        }

        public string GpuHeaderText => "GPU:";
        public string GpuUsageDisplayText => $"{Math.Round(ActiveGpuPercent, 1, MidpointRounding.AwayFromZero):0.#}%";

        private double _ramUsage;
        public double RamUsage
        {
            get => _ramUsage;
            private set
            {
                if (Math.Abs(_ramUsage - value) < 0.1) return;
                _ramUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RamUsageText));
            }
        }

        private bool _ramUsageIsStale;
        public bool RamUsageIsStale
        {
            get => _ramUsageIsStale;
            private set
            {
                if (_ramUsageIsStale == value) return;
                _ramUsageIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _ramUsageLastUpdatedUtc;
        public DateTime? RamUsageLastUpdatedUtc
        {
            get => _ramUsageLastUpdatedUtc;
            private set
            {
                if (_ramUsageLastUpdatedUtc == value) return;
                _ramUsageLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RamUsageText));
            }
        }

        public string RamUsageText => FormatMetric(RamUsage, RamUsageLastUpdatedUtc, "%");

        private double _diskUsage;
        public double DiskUsage
        {
            get => _diskUsage;
            private set
            {
                if (Math.Abs(_diskUsage - value) < 0.1) return;
                _diskUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiskUsageText));
            }
        }

        private bool _diskUsageIsStale;
        public bool DiskUsageIsStale
        {
            get => _diskUsageIsStale;
            private set
            {
                if (_diskUsageIsStale == value) return;
                _diskUsageIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _diskUsageLastUpdatedUtc;
        public DateTime? DiskUsageLastUpdatedUtc
        {
            get => _diskUsageLastUpdatedUtc;
            private set
            {
                if (_diskUsageLastUpdatedUtc == value) return;
                _diskUsageLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiskUsageText));
            }
        }

        public string DiskUsageText => FormatMetric(DiskUsage, DiskUsageLastUpdatedUtc, "%");

        private double _cpuTemp;
        public double CpuTemp
        {
            get => _cpuTemp;
            private set
            {
                if (Math.Abs(_cpuTemp - value) < 0.1) return;
                _cpuTemp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuTempText));
                OnPropertyChanged(nameof(CpuTempDisplay));
            }
        }

        private bool _cpuTempIsStale;
        public bool CpuTempIsStale
        {
            get => _cpuTempIsStale;
            private set
            {
                if (_cpuTempIsStale == value) return;
                _cpuTempIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _cpuTempLastUpdatedUtc;
        public DateTime? CpuTempLastUpdatedUtc
        {
            get => _cpuTempLastUpdatedUtc;
            private set
            {
                if (_cpuTempLastUpdatedUtc == value) return;
                _cpuTempLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuTempText));
                OnPropertyChanged(nameof(CpuTempDisplay));
            }
        }

        public string CpuTempText => FormatMetric(CpuTemp, CpuTempLastUpdatedUtc, "°C");
        public string CpuTempDisplay => $"Temp: {CpuTempText}";

        private double _gpuTemp;
        public double GpuTemp
        {
            get => _gpuTemp;
            private set
            {
                if (Math.Abs(_gpuTemp - value) < 0.1) return;
                _gpuTemp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuTempText));
                OnPropertyChanged(nameof(GpuTempDisplay));
            }
        }

        private bool _gpuTempIsStale;
        public bool GpuTempIsStale
        {
            get => _gpuTempIsStale;
            private set
            {
                if (_gpuTempIsStale == value) return;
                _gpuTempIsStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _gpuTempLastUpdatedUtc;
        public DateTime? GpuTempLastUpdatedUtc
        {
            get => _gpuTempLastUpdatedUtc;
            private set
            {
                if (_gpuTempLastUpdatedUtc == value) return;
                _gpuTempLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuTempText));
                OnPropertyChanged(nameof(GpuTempDisplay));
            }
        }

        public string GpuTempText => FormatMetric(GpuTemp, GpuTempLastUpdatedUtc, "°C");
        public string GpuTempDisplay => $"Temp: {GpuTempText}";

        private double _diskTemp;
        public double DiskTemp
        {
            get => _diskTemp;
            private set
            {
                if (Math.Abs(_diskTemp - value) < 0.1) return;
                _diskTemp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiskTempText));
                OnPropertyChanged(nameof(DiskTempDisplay));
            }
        }

        private bool _diskTempIsStale;
        public bool DiskTempIsStale
        {
            get => _diskTempIsStale;
            private set
            {
                if (_diskTempIsStale == value) return;
                _diskTempIsStale = value;
                OnPropertyChanged();
            }
        }

        private bool _isKeyMetricStale;
        public bool IsKeyMetricStale
        {
            get => _isKeyMetricStale;
            private set
            {
                if (_isKeyMetricStale == value) return;
                _isKeyMetricStale = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _diskTempLastUpdatedUtc;
        public DateTime? DiskTempLastUpdatedUtc
        {
            get => _diskTempLastUpdatedUtc;
            private set
            {
                if (_diskTempLastUpdatedUtc == value) return;
                _diskTempLastUpdatedUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiskTempText));
                OnPropertyChanged(nameof(DiskTempDisplay));
            }
        }

        public string DiskTempText => FormatMetric(DiskTemp, DiskTempLastUpdatedUtc, "°C");
        public string DiskTempDisplay => $"Temp: {DiskTempText}";

        private bool _avatarTelemetryIsStale;
        public bool AvatarTelemetryIsStale
        {
            get => _avatarTelemetryIsStale;
            private set
            {
                if (_avatarTelemetryIsStale == value) return;
                _avatarTelemetryIsStale = value;
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
            var now = DateTime.UtcNow;
            LastUpdateTimeUtc = now;
            UpdateTelemetrySchedule(_systemMonitoring?.NextTelemetryUpdateUtc);
            DataAge = TimeSpan.Zero;
            var cpuUsage = snapshot.CpuUsage;
            if (IsInvalid(cpuUsage))
            {
                CpuUsageIsStale = true;
                cpuUsage = (float)CpuUsage;
            }
            else
            {
                CpuUsage = cpuUsage;
                CpuUsageIsStale = false;
                CpuUsageLastUpdatedUtc = now;
            }

            var gpuUsage = snapshot.GpuUsage;
            if (IsInvalid(gpuUsage))
            {
                GpuUsageIsStale = true;
                gpuUsage = (float)GpuUsage;
            }
            else
            {
                GpuUsage = gpuUsage;
                GpuUsageIsStale = false;
                GpuUsageLastUpdatedUtc = now;
                ActiveGpuPercent = gpuUsage;
                ActiveGpuName = "GPU";
            }

            var ramUsage = snapshot.RamUsage;
            if (IsInvalid(ramUsage))
            {
                RamUsageIsStale = true;
                ramUsage = (float)RamUsage;
            }
            else
            {
                RamUsage = ramUsage;
                RamUsageIsStale = false;
                RamUsageLastUpdatedUtc = now;
            }

            var diskUsage = snapshot.DiskUsage;
            if (IsInvalid(diskUsage))
            {
                DiskUsageIsStale = true;
                diskUsage = (float)DiskUsage;
            }
            else
            {
                DiskUsage = diskUsage;
                DiskUsageIsStale = false;
                DiskUsageLastUpdatedUtc = now;
            }

            var cpuTemp = snapshot.CpuTemperature;
            if (IsInvalid(cpuTemp))
            {
                CpuTempIsStale = true;
                cpuTemp = (float)CpuTemp;
            }
            else
            {
                CpuTemp = cpuTemp;
                CpuTempIsStale = false;
                CpuTempLastUpdatedUtc = now;
            }

            var gpuTemp = snapshot.GpuTemperature;
            if (IsInvalid(gpuTemp))
            {
                GpuTempIsStale = true;
                gpuTemp = (float)GpuTemp;
            }
            else
            {
                GpuTemp = gpuTemp;
                GpuTempIsStale = false;
                GpuTempLastUpdatedUtc = now;
            }

            var diskTemp = snapshot.DiskTemperature;
            if (IsInvalid(diskTemp))
            {
                DiskTempIsStale = true;
                diskTemp = (float)DiskTemp;
            }
            else
            {
                DiskTemp = diskTemp;
                DiskTempIsStale = false;
                DiskTempLastUpdatedUtc = now;
            }

            UpdateKeyMetricStaleness();

#if DEBUG
            if (UseDebugStress)
            {
                AvatarTelemetry.SetStress(_debugStress);
            }
            else
            {
                AvatarTelemetry.UpdateFromMetrics(
                    cpuUsage,
                    gpuUsage,
                    ramUsage,
                    cpuTemp,
                    gpuTemp,
                    diskTemp);
            }
#else
            AvatarTelemetry.UpdateFromMetrics(
                cpuUsage,
                gpuUsage,
                ramUsage,
                cpuTemp,
                gpuTemp,
                diskTemp);
#endif

            UpdateMood(cpuUsage, gpuUsage, ramUsage, diskUsage,
                cpuTemp, gpuTemp, diskTemp);
        }

        private void ApplySnapshot(MetricsEventArgs metrics)
        {
            LastUpdateTimeUtc = metrics.SampledAtUtc;
            UpdateTelemetrySchedule(_legacyMonitoring?.NextTelemetryUpdateUtc);
            DataAge = metrics.DataAge;
            var cpuUsage = metrics.CpuUsage;
            if (IsInvalid(cpuUsage))
            {
                CpuUsageIsStale = true;
                cpuUsage = CpuUsage;
                CpuUsageLastUpdatedUtc = metrics.CpuUsageLastUpdatedUtc;
            }
            else
            {
                CpuUsage = cpuUsage;
                CpuUsageIsStale = metrics.CpuUsageIsStale;
                CpuUsageLastUpdatedUtc = metrics.CpuUsageLastUpdatedUtc;
            }

            var gpuUsage = metrics.GpuUsage;
            if (IsInvalid(gpuUsage))
            {
                GpuUsageIsStale = true;
                gpuUsage = GpuUsage;
                GpuUsageLastUpdatedUtc = metrics.GpuUsageLastUpdatedUtc;
                ActiveGpuPercent = metrics.ActiveGpuPercent ?? gpuUsage;
                ActiveGpuName = metrics.ActiveGpuName ?? ActiveGpuName;
            }
            else
            {
                GpuUsage = gpuUsage;
                GpuUsageIsStale = metrics.GpuUsageIsStale;
                GpuUsageLastUpdatedUtc = metrics.GpuUsageLastUpdatedUtc;
                ActiveGpuPercent = metrics.ActiveGpuPercent ?? gpuUsage;
                ActiveGpuName = metrics.ActiveGpuName ?? "GPU";
            }

            var ramUsage = metrics.RamUsage;
            if (IsInvalid(ramUsage))
            {
                RamUsageIsStale = true;
                ramUsage = RamUsage;
                RamUsageLastUpdatedUtc = metrics.RamUsageLastUpdatedUtc;
            }
            else
            {
                RamUsage = ramUsage;
                RamUsageIsStale = metrics.RamUsageIsStale;
                RamUsageLastUpdatedUtc = metrics.RamUsageLastUpdatedUtc;
            }

            var diskUsage = metrics.DiskUsage;
            if (IsInvalid(diskUsage))
            {
                DiskUsageIsStale = true;
                diskUsage = DiskUsage;
                DiskUsageLastUpdatedUtc = metrics.DiskUsageLastUpdatedUtc;
            }
            else
            {
                DiskUsage = diskUsage;
                DiskUsageIsStale = metrics.DiskUsageIsStale;
                DiskUsageLastUpdatedUtc = metrics.DiskUsageLastUpdatedUtc;
            }

            var cpuTemp = metrics.CpuTemp;
            if (IsInvalid(cpuTemp))
            {
                CpuTempIsStale = true;
                cpuTemp = CpuTemp;
                CpuTempLastUpdatedUtc = metrics.CpuTempLastUpdatedUtc;
            }
            else
            {
                CpuTemp = cpuTemp;
                CpuTempIsStale = metrics.CpuTempIsStale;
                CpuTempLastUpdatedUtc = metrics.CpuTempLastUpdatedUtc;
            }

            var gpuTemp = metrics.GpuTemp;
            if (IsInvalid(gpuTemp))
            {
                GpuTempIsStale = true;
                gpuTemp = GpuTemp;
                GpuTempLastUpdatedUtc = metrics.GpuTempLastUpdatedUtc;
            }
            else
            {
                GpuTemp = gpuTemp;
                GpuTempIsStale = metrics.GpuTempIsStale;
                GpuTempLastUpdatedUtc = metrics.GpuTempLastUpdatedUtc;
            }

            var diskTemp = metrics.DiskTemp;
            if (IsInvalid(diskTemp))
            {
                DiskTempIsStale = true;
                diskTemp = DiskTemp;
                DiskTempLastUpdatedUtc = metrics.DiskTempLastUpdatedUtc;
            }
            else
            {
                DiskTemp = diskTemp;
                DiskTempIsStale = metrics.DiskTempIsStale;
                DiskTempLastUpdatedUtc = metrics.DiskTempLastUpdatedUtc;
            }

            UpdateKeyMetricStaleness();

#if DEBUG
            if (UseDebugStress)
            {
                AvatarTelemetry.SetStress(_debugStress);
            }
            else
            {
                AvatarTelemetry.UpdateFromMetrics(
                    cpuUsage,
                    gpuUsage,
                    ramUsage,
                    cpuTemp,
                    gpuTemp,
                    diskTemp);
            }
#else
            AvatarTelemetry.UpdateFromMetrics(
                cpuUsage,
                gpuUsage,
                ramUsage,
                cpuTemp,
                gpuTemp,
                diskTemp);
#endif

            UpdateMood(cpuUsage, gpuUsage, ramUsage, diskUsage,
                cpuTemp, gpuTemp, diskTemp);
        }

        private void UpdateMood(double cpu, double gpu, double ram, double disk, double cpuTemp, double gpuTemp, double diskTemp)
        {
            var stats = new SystemStats(cpu, gpu, ram, disk, cpuTemp, gpuTemp, diskTemp);
            CurrentMood = MoodEngine.FromStats(stats);
        }

        private void UpdateKeyMetricStaleness()
        {
            IsKeyMetricStale = CpuUsageIsStale
                || GpuUsageIsStale
                || RamUsageIsStale
                || DiskUsageIsStale
                || CpuTempIsStale
                || GpuTempIsStale
                || DiskTempIsStale;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static bool IsInvalid(double value)
            => !TelemetrySanitizer.IsValid(value);

        private static bool IsInvalid(float value)
            => !TelemetrySanitizer.IsValid(value);

        private void UpdateAvatarTelemetryStale()
            => AvatarTelemetryIsStale = CpuUsageIsStale
                || GpuUsageIsStale
                || RamUsageIsStale
                || DiskUsageIsStale
                || CpuTempIsStale
                || GpuTempIsStale
                || DiskTempIsStale;

        private static string FormatMetric(double value, DateTime? lastUpdatedUtc, string unit)
        {
            var displayValue = lastUpdatedUtc.HasValue ? value : double.NaN;
            return TelemetrySanitizer.FormatOptional(displayValue, unit);
        }

        private DispatcherTimer? CreateCooldownTimer()
        {
            if (Application.Current?.Dispatcher is null)
            {
                return null;
            }

            var timer = new DispatcherTimer(
                TimeSpan.FromSeconds(1),
                DispatcherPriority.Background,
                (_, _) => OnCooldownTimerTick(),
                Application.Current.Dispatcher);
            timer.Start();
            return timer;
        }

        private void OnCooldownTimerTick()
        {
            RefreshTelemetryScheduleFromService();
            RecalculateTelemetryCooldown();
        }

        private void RefreshTelemetryScheduleFromService()
        {
            var nextUpdate = _legacyMonitoring?.NextTelemetryUpdateUtc
                ?? _systemMonitoring?.NextTelemetryUpdateUtc;
            if (nextUpdate.HasValue && nextUpdate == _nextTelemetryUpdateUtc)
            {
                return;
            }

            if (nextUpdate.HasValue)
            {
                UpdateTelemetrySchedule(nextUpdate);
            }
        }

        private void UpdateTelemetrySchedule(DateTime? nextUpdateUtc)
        {
            if (_nextTelemetryUpdateUtc == nextUpdateUtc)
            {
                RecalculateTelemetryCooldown();
                return;
            }

            _nextTelemetryUpdateUtc = nextUpdateUtc;
            OnPropertyChanged(nameof(NextTelemetryUpdateUtc));
            if (_lastUpdateTimeUtc.HasValue && nextUpdateUtc.HasValue && nextUpdateUtc.Value > _lastUpdateTimeUtc.Value)
            {
                _telemetryCooldownDuration = nextUpdateUtc.Value - _lastUpdateTimeUtc.Value;
            }

            RecalculateTelemetryCooldown();
        }

        private void RecalculateTelemetryCooldown()
        {
            if (_nextTelemetryUpdateUtc.HasValue)
            {
                var remaining = _nextTelemetryUpdateUtc.Value - DateTime.UtcNow;
                TelemetryCooldownRemaining = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
            else
            {
                TelemetryCooldownRemaining = TimeSpan.Zero;
            }

            UpdateTelemetryCooldownState();
        }

        private void UpdateTelemetryCooldownState()
        {
            if (!_lastUpdateTimeUtc.HasValue || !_telemetryCooldownDuration.HasValue || _telemetryCooldownDuration.Value <= TimeSpan.Zero)
            {
                TelemetryCooldownState = TelemetryCooldownStates.Unknown;
                return;
            }

            var age = DateTime.UtcNow - _lastUpdateTimeUtc.Value;
            var ratio = age.TotalSeconds / _telemetryCooldownDuration.Value.TotalSeconds;
            if (ratio <= 0.5)
            {
                TelemetryCooldownState = TelemetryCooldownStates.Fresh;
            }
            else if (ratio <= 0.85)
            {
                TelemetryCooldownState = TelemetryCooldownStates.Warning;
            }
            else
            {
                TelemetryCooldownState = TelemetryCooldownStates.Stale;
            }
        }

        private static string FormatCooldown(TimeSpan remaining)
        {
            var totalMinutes = Math.Max(0, (int)remaining.TotalMinutes);
            var seconds = Math.Max(0, remaining.Seconds);
            return $"{totalMinutes:00}:{seconds:00}";
        }

        private static class TelemetryCooldownStates
        {
            public const string Unknown = "Unknown";
            public const string Fresh = "Fresh";
            public const string Warning = "Warning";
            public const string Stale = "Stale";
        }
    }
}
