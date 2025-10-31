using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private CancellationTokenSource? _monCts;

        private void StartSurveillance()
        {
            _monCts?.Cancel();
            _monCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monCts.Token);
        }

        private void StopSurveillance()
        {
            _monCts?.Cancel();
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // TODO: brancher sur ton service capteurs réel
                await Dispatcher.InvokeAsync(() =>
                {
                    CpuBar.Value = 20;  // remplacer par vraies valeurs
                    RamBar.Value = 30;
                    GpuBar.Value = 15;
                    DiskBar.Value = 5;
                });

                await Task.Delay(1500, ct);
            }
        }
    }
}
