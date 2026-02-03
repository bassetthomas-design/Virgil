using System;

namespace Virgil.App.Services;

public sealed class ActionProgressReporter : IProgress<double>
{
    private readonly ActionProgressService _progressService;

    public ActionProgressReporter(ActionProgressService progressService)
    {
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
    }

    public void Report(double value)
    {
        _progressService.Report(value);
    }
}
