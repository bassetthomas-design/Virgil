using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Lecture ponctuelle de métriques "avancées" côté matériel.
    /// - CPU temp: tentative via WMI (MSAcpi_ThermalZoneTemperature) → souvent null sur PC modernes
    /// - GPU temp: tentative via nvidia-smi si présent (NVIDIA). Sinon null.
    /// - Disk temp: non fiable sans smartctl → null par défaut (peut être ajouté plus tard).
    /// Ne garde pas d'état ; crée une instance, appelle Read(), puis Dispose().
    /// </summary>
    public sealed class AdvancedMonitoringService : IDisposable
    {
        public HardwareSnapshot Read()
        {
            var snap = new HardwareSnapshot
            {
                CpuTempC = TryReadCpuTempC(),
                GpuTempC = TryReadGpuTempC(),
                DiskTempC = null // TODO: intégrer smartctl/LibreHardwareMonitor plus tard
            };
            return snap;
        }

        private float? TryReadCpuTempC()
        {
            try
            {
                // WMI MSAcpi_ThermalZoneTemperature → Kelvin * 10 (souvent non renseigné)
                var output = RunProcessCapture("wmic",
                    @"/namespace:\\root\wmi PATH MSAcpi_ThermalZoneTemperature get CurrentTemperature /value");
                // Exemple: CurrentTemperature=3050  (=> 305.0 Kelvin -> 31.85°C)
                if (string.IsNullOrWhiteSpace(output)) return null;
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var idx = line.IndexOf("CurrentTemperature=", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var raw = line.Substring(idx + "CurrentTemperature=".Length).Trim();
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tenthKelvin))
                        {
                            var kelvin = tenthKelvin / 10.0f;
                            var c = kelvin - 273.15f;
                            if (c > -100 && c < 150) return c;
                        }
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private float? TryReadGpuTempC()
        {
            try
            {
                // NVIDIA: nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits
                var nvsmi = "nvidia-smi";
                var output = RunProcessCapture(nvsmi,
                    "--query-gpu=temperature.gpu --format=csv,noheader,nounits");
                if (string.IsNullOrWhiteSpace(output)) return null;

                var line = output.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (float.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
                {
                    if (temp > -50 && temp < 150) return temp;
                }
                return null;
            }
            catch { return null; }
        }

        private static string RunProcessCapture(string fileName, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var sb = new StringBuilder();
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[proc error] {ex.Message}";
            }
        }

        public void Dispose() { /* rien à disposer pour l’instant */ }
    }
}
