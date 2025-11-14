using Microsoft.Extensions.DependencyInjection;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Méthodes d’extension pour enregistrer les services de base de Virgil.
/// À appeler depuis le démarrage de l’application (Virgil.App).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVirgilCore(this IServiceCollection services)
    {
        // Orchestrateur central
        services.AddSingleton<IActionOrchestrator, ActionOrchestrator>();

        // Services métier
        services.AddSingleton<ICleanupService, CleanupService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<IPerformanceService, PerformanceService>();
        services.AddSingleton<IDiagnosticService, DiagnosticService>();
        services.AddSingleton<ISpecialService, SpecialService>();
        services.AddSingleton<IChatService, ChatService>();

        return services;
    }
}
