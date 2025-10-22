using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace Virgil.Core.Services
{
    public sealed class HardwareSnapshot
    {
        public double? CpuTempC { get; set; }
        public double? GpuTempC { get; set; }
        public double? DiskTempC { get; set; }
        public Dictionary<string, double?> Extra { get; } = new();
    }

    public sealed class AdvancedMonitoringService : IVisitor, IDisposable
    {
        private readonly Computer _pc;

        public AdvancedMonitoringService()
        {
            _pc = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsMemoryEnabled = false
            };
            _pc.Open();
        }

        public HardwareSnapshot Read()
        {
            var snap = new HardwareSnapshot();

            _pc.Accept(this); // Update sensors

            foreach (var hw in _pc.Hardware)
            {
                TryRead(hw, snap);
                foreach (var sub in hw.SubHardware)
                    TryRead(sub, snap);
            }

            return snap;
        }

        private static void TryRead(IHardware hw, HardwareSnapshot snap)
        {
            hw.Update();
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == SensorType.Temperature)
                {
                    var name = (s.Name ?? "").ToLowerInvariant();
                    var value = s.Value;

                    if (value.HasValue)
                    {
                        if (name.Contains("cpu") && snap.CpuTempC is null)
                            snap.CpuTempC = value.Value;
                        else if ((name.Contains("gpu") || hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel) && snap.GpuTempC is null)
                            snap.GpuTempC = value.Value;
                        else if ((name.Contains("ssd") || name.Contains("hdd") || name.Contains("drive") || hw.HardwareType == HardwareType.Storage) && snap.DiskTempC is null)
                            snap.DiskTempC = value.Value;
                        else
                            snap.Extra[s.Identifier.ToString()] = value.Value;
                    }
                }
            }
        }

        public void VisitComputer(IComputer computer) { }
        public void VisitHardware(IHardware hardware) { hardware.Update(); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }

        public void Dispose() { try { _pc.Close(); } catch { } }
    }
}
