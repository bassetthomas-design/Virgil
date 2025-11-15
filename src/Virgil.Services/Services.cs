using Virgil.App.Services;

namespace Virgil.Services
{
    /// <summary>
    /// Static access point for services that need to interact with the monitoring pipeline.
    /// This minimal version only exposes the contract required by the dev branch so that
    /// the Core project can compile while the full infrastructure is evolving.
    /// </summary>
    public static class Services
    {
        /// <summary>
        /// Gets or sets the global monitoring service instance.
        /// The concrete implementation is provided by the host (Virgil.App).
        /// </summary>
        public static IMonitoringService? Monitoring { get; set; }
    }
}
