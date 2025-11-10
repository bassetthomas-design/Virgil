using System;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public class ActionsService
    {
        public bool SurveillanceOn { get; private set; }

        public Task ToggleSurveillanceAsync(Func<Task> onStart, Func<Task> onStop)
        {
            if (!SurveillanceOn) { SurveillanceOn = true; return onStart(); }
            SurveillanceOn = false; return onStop();
        }

        public Task MaintenanceCompleteAsync() => Task.CompletedTask;
        public Task SmartCleanupAsync() => Task.CompletedTask;
        public Task CleanBrowsersAsync() => Task.CompletedTask;
        public Task UpdateAllAsync() => Task.CompletedTask;
        public Task DefenderUpdateAndScanAsync() => Task.CompletedTask;
        public Task OpenConfigAsync() => Task.CompletedTask;
    }
}
