namespace Virgil.App.Services
{
    /// <summary>
    /// Shim interface for IMonitoringService so that shared services compiled from the
    /// Virgil.Services folder can reference it without requiring the full App project.
    /// This keeps the dev branch build green while the final architecture is still
    /// being wired. The real implementation lives in Virgil.App.
    /// </summary>
    public interface IMonitoringService
    {
    }
}
