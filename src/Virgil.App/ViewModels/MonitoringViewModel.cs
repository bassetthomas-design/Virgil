using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public class MonitoringViewModel : INotifyPropertyChanged
{
    private readonly IMonitoringService _service;

    public MonitoringViewModel(IMonitoringService service)
    {
        _service = service;
        _service.Updated += OnUpdated;
    }

    private double _cpuUsage; public double CpuUsage { get => _cpuUsage; set { _cpuUsage = value; OnPropertyChanged(); } }
    private double _cpuTemp;  public double CpuTemp  { get => _cpuTemp;  set { _cpuTemp  = value; OnPropertyChanged(); } }
    private double _gpuUsage; public double GpuUsage { get => _gpuUsage; set { _gpuUsage = value; OnPropertyChanged(); } }
    private double _gpuTemp;  public double GpuTemp  { get => _gpuTemp;  set { _gpuTemp  = value; OnPropertyChanged(); } }
    private double _ramUsage; public double RamUsage { get => _ramUsage; set { _ramUsage = value; OnPropertyChanged(); } }
    private double _diskUsage;public double DiskUsage{ get => _diskUsage;set { _diskUsage= value; OnPropertyChanged(); } }
    private double _diskTemp; public double DiskTemp { get => _diskTemp; set { _diskTemp = value; OnPropertyChanged(); } }

    private bool _isRunning; public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }

    private void OnUpdated(object? s, IMonitoringService.Snapshot snap)
    {
        CpuUsage = snap.CpuUsage; CpuTemp = snap.CpuTemp;
        GpuUsage = snap.GpuUsage; GpuTemp = snap.GpuTemp;
        RamUsage = snap.RamUsage; DiskUsage = snap.DiskUsage; DiskTemp = snap.DiskTemp;
    }

    public void Start(){ _service.Start(); IsRunning = true; }
    public void Stop(){ _service.Stop(); IsRunning = false; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n=null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
