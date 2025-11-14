using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Virgil.App.ViewModels;

public sealed class MainShellLot1DemoViewModel : INotifyPropertyChanged
{
    private bool _isMonitoringEnabled = true;
    private string _currentTime = "12:34:56";

    public string Title => "Virgil – Demo Lot 1";

    public string CurrentTime
    {
        get => _currentTime;
        set
        {
            if (value == _currentTime) return;
            _currentTime = value;
            OnPropertyChanged();
        }
    }

    public bool IsMonitoringEnabled
    {
        get => _isMonitoringEnabled;
        set
        {
            if (value == _isMonitoringEnabled) return;
            _isMonitoringEnabled = value;
            OnPropertyChanged();
        }
    }

    public string CpuUsage => "32 %";
    public string GpuUsage => "18 %";
    public string RamUsage => "42 %";
    public string DiskUsage => "9 %";

    public string CpuTemp => "54 °C";
    public string GpuTemp => "49 °C";
    public string DiskTemp => "37 °C";

    public string Mood => "Happy";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}