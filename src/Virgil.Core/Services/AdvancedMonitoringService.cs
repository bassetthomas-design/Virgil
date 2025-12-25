using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using LibreHardwareMonitor.Hardware;
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
                DiskTempC = TryReadDiskTempC() // lecture via LibreHardwareMonitor; null si indisponible
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

        /// <summary>
        /// Essaie de lire la température du disque principal via LibreHardwareMonitor.
        /// Retourne la température maximale trouvée parmi les disques ou null si aucune n'est disponible.
        /// </summary>
        private float? TryReadDiskTempC()
        {
            try
            {
                // Crée un objet Computer avec uniquement le stockage activé pour minimiser l'overhead.
                using var pc = new Computer { IsStorageEnabled = true };
                pc.Open();

                float? max = null;
                foreach (var hw in pc.Hardware)
                {
                    if (hw.HardwareType != HardwareType.Storage) continue;
                    hw.Update();
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature)
                        {
                            // s.Value est un float? ; ignorer les valeurs nulles et absurdes
                            var val = s.Value;
                            if (val.HasValue && val.Value > -50 && val.Value < 150)
                            {
                                if (!max.HasValue || val.Value > max.Value)
                                    max = val.Value;
                            }
                        }
                    }
                }
                return max;
            }
            catch
            {
                // Retourne null en cas d'erreur (par exemple bibliothèque non disponible ou pas de disque SMART)
                return null;
            }
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
