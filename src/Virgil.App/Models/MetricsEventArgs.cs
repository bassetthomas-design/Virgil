using System;

namespace Virgil.App.Models
{
    public class MetricsEventArgs : EventArgs
    {
        public double Cpu { get; }
        public double Gpu { get; }
        public double Ram { get; }
        public double Temp { get; }

        public MetricsEventArgs(double cpu, double gpu, double ram, double temp)
        {
            Cpu = cpu; Gpu = gpu; Ram = ram; Temp = temp;
        }
    }
}
