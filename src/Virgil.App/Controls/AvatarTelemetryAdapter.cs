using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Virgil.App.Controls;

public sealed class AvatarTelemetryAdapter : INotifyPropertyChanged
{
    private double _stress;

    public double Stress
    {
        get => _stress;
        private set
        {
            var clamped = Math.Clamp(value, 0d, 1d);
            if (Math.Abs(_stress - clamped) < 0.0001)
            {
                return;
            }

            _stress = clamped;
            OnPropertyChanged();
        }
    }

    public void SetStress(double stress) => Stress = stress;

    public void UpdateFromMetrics(double cpuUsage, double gpuUsage, double ramUsage, double cpuTemp, double gpuTemp, double diskTemp)
    {
        var usageStress = Math.Max(cpuUsage, Math.Max(gpuUsage, ramUsage)) / 100d;
        var tempStress = Math.Max(cpuTemp, Math.Max(gpuTemp, diskTemp)) / 100d;
        SetStress(Math.Clamp(Math.Max(usageStress, tempStress), 0d, 1d));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
