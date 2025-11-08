using System;

namespace Virgil.App.Services;

// Stub: planificateur local (mémoire) – branchement futur sur schtasks.exe / Task Scheduler API
public class LocalMaintenanceScheduler : IMaintenanceScheduler
{
    public DateTime? NextPlannedRun { get; private set; }

    public void PlanDaily(TimeSpan atTimeLocal)
    {
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, atTimeLocal.Hours, atTimeLocal.Minutes, 0);
        if (next <= now) next = next.AddDays(1);
        NextPlannedRun = next;
    }

    public void Cancel() => NextPlannedRun = null;
}
