namespace Virgil.Services;

public enum RamboErrorDecision
{
    Continue,
    Stop
}

public sealed class RamboErrorDialogResult
{
    public RamboErrorDecision Decision { get; set; }

    public bool AutoContinueSimilarErrors { get; set; }
}

