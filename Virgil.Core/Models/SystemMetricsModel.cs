using System.ComponentModel;

namespace Virgil.Core.Models
{
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
            set { _cpuUsage = value; OnPropertyChanged(nameof(CpuUsage)); }
        }

        public float RamUsage
        {
            get => _ramUsage;
            set { _ramUsage = value; OnPropertyChanged(nameof(RamUsage)); }
        }

        public float CpuTemp
        {
            get => _cpuTemp;
            set { _cpuTemp = value; OnPropertyChanged(nameof(CpuTemp)); }
        }

        public float GpuTemp
        {
            get => _gpuTemp;
            set { _gpuTemp = value; OnPropertyChanged(nameof(GpuTemp)); }
        }

        public float DiskUsage
        {
            get => _diskUsage;
            set { _diskUsage = value; OnPropertyChanged(nameof(DiskUsage)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
