using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Core;
using Virgil.App.Models;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public class MonitoringViewModel : INotifyPropertyChanged
    {
        private readonly MonitoringService _mon;

        public MonitoringViewModel(MonitoringService mon)
        {
            _mon = mon;
            _mon.Updated += OnUpdated;
            _mon.Start();
        }

        public void HookMood(MoodMapper mapper)
            => mapper.MoodChanged += m => Mood = m;

        private double _cpuUsage;
        public double CpuUsage { get => _cpuUsage; set { if (Set(ref _cpuUsage, value)) OnPropertyChanged(); } }

        private double _cpuTemp;
        public double CpuTemp { get => _cpuTemp; set { if (Set(ref _cpuTemp, value)) OnPropertyChanged(); } }

        private double _gpuUsage;
        public double GpuUsage { get => _gpuUsage; set { if (Set(ref _gpuUsage, value)) OnPropertyChanged(); } }

        private double _gpuTemp;
        public double GpuTemp { get => _gpuTemp; set { if (Set(ref _gpuTemp, value)) OnPropertyChanged(); } }

        private double _ramUsage;
        public double RamUsage { get => _ramUsage; set { if (Set(ref _ramUsage, value)) OnPropertyChanged(); } }

        private double _diskUsage;
        public double DiskUsage { get => _diskUsage; set { if (Set(ref _diskUsage, value)) OnPropertyChanged(); } }

        private double _diskTemp;
        public double DiskTemp { get => _diskTemp; set { if (Set(ref _diskTemp, value)) OnPropertyChanged(); } }

        private MoodState _mood = MoodState.Focused;
        public MoodState Mood { get => _mood; set { if (Set(ref _mood, value)) OnPropertyChanged(); } }

        private void OnUpdated(object? sender, MetricsEventArgs e)
        {
            CpuUsage = e.CpuUsage;
            CpuTemp  = e.CpuTemp;
            GpuUsage = e.GpuUsage;
            GpuTemp  = e.GpuTemp;
            RamUsage = e.RamUsage;
            DiskUsage = e.DiskUsage;
            DiskTemp  = e.DiskTemp;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            return true;
        }
    }
}
