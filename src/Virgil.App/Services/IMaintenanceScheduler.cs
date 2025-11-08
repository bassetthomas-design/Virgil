using System;

namespace Virgil.App.Services;

public interface IMaintenanceScheduler
{
    DateTime? NextPlannedRun { get; }
    void PlanDaily(TimeSpan atTimeLocal);
    void Cancel();
}