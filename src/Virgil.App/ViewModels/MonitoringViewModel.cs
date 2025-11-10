using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public sealed class MonitoringViewModel : INotifyPropertyChanged
{
    private readonly MonitoringService _svc;

    public MonitoringViewModel(MonitoringService svc)
    {
        _svc = svc;
        _svc.Updated += (_, snap) =>
        {
            CpuUsage = snap.CpuUsage; CpuTemp = snap.CpuTemp;
            GpuUsage = snap.GpuUsage; GpuTemp = snap.GpuTemp;
            RamUsage = snap.RamUsage; DiskUsage = snap.DiskUsage; DiskTemp = snap.DiskTemp;
        };
    }

    private double _cpuUsage; public double CpuUsage { get=>_cpuUsage; private set{ _cpuUsage=value; OnPropertyChanged(); }}
    private double _cpuTemp;  public double CpuTemp  { get=>_cpuTemp;  private set{ _cpuTemp=value;  OnPropertyChanged(); }}
    private double _gpuUsage; public double GpuUsage { get=>_gpuUsage; private set{ _gpuUsage=value; OnPropertyChanged(); }}
    private double _gpuTemp;  public double GpuTemp  { get=>_gpuTemp;  private set{ _gpuTemp=value;  OnPropertyChanged(); }}
    private double _ramUsage; public double RamUsage { get=>_ramUsage; private set{ _ramUsage=value; OnPropertyChanged(); }}
    private double _diskUsage;public double DiskUsage{ get=>_diskUsage;private set{ _diskUsage=value;OnPropertyChanged(); }}
    private double _diskTemp; public double DiskTemp { get=>_diskTemp; private set{ _diskTemp=value; OnPropertyChanged(); }}

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
