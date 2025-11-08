using LibreHardwareMonitor.Hardware;

namespace Virgil.App.Services;

public class GpuService : IGpuService
{
    private readonly Computer _comp;

    public GpuService()
    {
        _comp = new Computer { IsGpuEnabled = true };
        _comp.Open();
    }

    public (double usage, double temp) Read()
    {
        double u = 0, t = 0;
        foreach (var hw in _comp.Hardware)
        {
            if (hw.HardwareType != HardwareType.GpuNvidia && hw.HardwareType != HardwareType.GpuAmd && hw.HardwareType != HardwareType.GpuIntel) continue;
            hw.Update();
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Core")) t = s.Value ?? t;
                if (s.SensorType == SensorType.Load && s.Name.Contains("Core")) u = s.Value ?? u;
            }
        }
        return (u, t);
    }
}
