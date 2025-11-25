namespace Virgil.Services
{
    /// <summary>
    /// Lightweight extension hook for registering Virgil services without taking a hard
    /// compile-time dependency on Microsoft.Extensions.DependencyInjection in this project.
    /// The concrete DI wiring can be done in the host application.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// No-op placeholder used to keep compilation stable on the dev branch.
        /// The generic signature allows this method to be used with any container type
        /// (for example an IServiceCollection in a DI-enabled host).
        /// </summary>
        public static T AddVirgilServices<T>(this T services)
        {
            // Intentionally left as a no-op: the registration logic belongs in the host layer.
            return services;
        }
    }
}
