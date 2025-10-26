using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to list and control Windows services. This class
    /// wraps the ServiceController API and handles common exceptions.
    /// </summary>
    public class ServiceManager
    {
        /// <summary>
        /// Lists all services installed on the system.
        /// </summary>
        public List<ServiceController> ListServices()
        {
            try
            {
                return ServiceController.GetServices().OrderBy(s => s.ServiceName).ToList();
            }
            catch
            {
                return new List<ServiceController>();
            }
        }

        /// <summary>
        /// Starts the specified service if it is stopped.
        /// </summary>
        public bool StartService(string serviceName)
        {
            try
            {
                var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stops the specified service if it is running and can be stopped.
        /// </summary>
        public bool StopService(string serviceName)
        {
            try
            {
                var sc = new ServiceController(serviceName);
                if (sc.CanStop && sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}