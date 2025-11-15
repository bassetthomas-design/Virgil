namespace Virgil.App.Services
{
    /// <summary>
    /// Shim interface for IMonitoringService used by shared code compiled in the Core project.
    /// The full monitoring contract and implementation live in the Virgil.App project.
    /// This placeholder only exists so that references from shared services compile on the
    /// dev branch without adding a reverse project reference from Core to App.
    /// </summary>
    public interface IMonitoringService
    {
    }
}
