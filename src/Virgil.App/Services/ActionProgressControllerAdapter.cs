using Virgil.Services.Abstractions;

namespace Virgil.App.Services;

public sealed class ActionProgressControllerAdapter : IActionProgressController
{
    private readonly ActionProgressService _progress;

    public ActionProgressControllerAdapter(ActionProgressService progress)
    {
        _progress = progress;
    }

    public void StartIndeterminate() => _progress.StartIndeterminate();

    public void Complete() => _progress.Complete();
}
