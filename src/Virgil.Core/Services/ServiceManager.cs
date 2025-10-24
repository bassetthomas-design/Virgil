using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;

namespace Virgil.Core.Services
{
    public sealed class ServiceInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
        public bool CanPauseAndContinue { get; set; }
        public bool CanShutdown { get; set; }
        public bool CanStop { get; set; }
    }

    public sealed class ServiceManager
    {
        public List<ServiceInfo> ListAll()
        {
            return ServiceController.GetServices()
                .OrderBy(s => s.DisplayName)
                .Select(s => new ServiceInfo
                {
                    Name = s.ServiceName,
                    DisplayName = s.DisplayName,
                    Status = s.Status.ToString(),
                    CanPauseAndContinue = s.CanPauseAndContinue,
                    CanShutdown = s.CanShutdown,
                    CanStop = s.CanStop
                })
                .ToList();
        }

        public bool Restart(string serviceName, TimeSpan? timeout = null)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                timeout ??= TimeSpan.FromSeconds(20);

                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    if (sc.CanStop) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout.Value); }
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, timeout.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Stop(string serviceName)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status != ServiceControllerStatus.Stopped && sc.CanStop)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
                return true;
            }
            catch { return false; }
        }

        public bool Start(string serviceName)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
                return true;
            }
            catch { return false; }
        }
    }
}
