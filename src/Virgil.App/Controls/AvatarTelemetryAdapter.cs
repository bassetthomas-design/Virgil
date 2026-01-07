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
            var clamped = ValueSanitizer.Sanitize01(value, 0d);
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
        var safeCpuUsage = SanitizeMetric(cpuUsage);
        var safeGpuUsage = SanitizeMetric(gpuUsage);
        var safeRamUsage = SanitizeMetric(ramUsage);
        var safeCpuTemp = SanitizeMetric(cpuTemp);
        var safeGpuTemp = SanitizeMetric(gpuTemp);
        var safeDiskTemp = SanitizeMetric(diskTemp);

        var usageStress = Math.Max(safeCpuUsage, Math.Max(safeGpuUsage, safeRamUsage)) / 100d;
        var tempStress = Math.Max(safeCpuTemp, Math.Max(safeGpuTemp, safeDiskTemp)) / 100d;
        var stress = Math.Max(usageStress, tempStress);

        SetStress(ValueSanitizer.Sanitize01(stress, 0d));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static double SanitizeMetric(double value)
        => double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
}
