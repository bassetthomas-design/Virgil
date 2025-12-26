using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Virgil.Core.Models
{
    /// <summary>
    /// Represents observable system metrics for UI binding scenarios.
    /// </summary>
    public class SystemMetricsModel : INotifyPropertyChanged
    {
        private float _cpuUsage;
        private float _ramUsage;
        private float _cpuTemp;
        private float _gpuTemp;
        private float _diskUsage;

        public float CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        public float RamUsage
        {
            get => _ramUsage;
            set => SetProperty(ref _ramUsage, value);
        }

        public float CpuTemp
        {
            get => _cpuTemp;
            set => SetProperty(ref _cpuTemp, value);
        }

        public float GpuTemp
        {
            get => _gpuTemp;
            set => SetProperty(ref _gpuTemp, value);
        }

        public float DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
