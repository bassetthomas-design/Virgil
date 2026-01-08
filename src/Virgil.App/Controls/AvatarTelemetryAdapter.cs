using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Virgil.App.Utils;

namespace Virgil.App.Controls;

public sealed class AvatarTelemetryAdapter : INotifyPropertyChanged
{
    private double _stress;

    public double Stress
    {
        get => _stress;
        private set
        {
            var clamped = TelemetrySanitizer.Clamp01(value, 0d);
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
        var safeCpuUsage = TelemetrySanitizer.OrFallback(cpuUsage, 0d);
        var safeGpuUsage = TelemetrySanitizer.OrFallback(gpuUsage, 0d);
        var safeRamUsage = TelemetrySanitizer.OrFallback(ramUsage, 0d);
        var safeCpuTemp = TelemetrySanitizer.OrFallback(cpuTemp, 0d);
        var safeGpuTemp = TelemetrySanitizer.OrFallback(gpuTemp, 0d);
        var safeDiskTemp = TelemetrySanitizer.OrFallback(diskTemp, 0d);

        var usageStress = Math.Max(safeCpuUsage, Math.Max(safeGpuUsage, safeRamUsage)) / 100d;
        var tempStress = Math.Max(safeCpuTemp, Math.Max(safeGpuTemp, safeDiskTemp)) / 100d;
        var stress = Math.Max(usageStress, tempStress);

        SetStress(TelemetrySanitizer.Clamp01(stress, 0d));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}
