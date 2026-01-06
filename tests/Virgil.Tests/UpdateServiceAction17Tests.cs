using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class UpdateServiceAction17Tests
{
    [Fact]
    public async Task ManageAutomaticUpdates_ReturnsStatusAndRecommendations()
    {
        var snapshot = new AutomaticUpdateSnapshot(
            Supported: true,
            AutomaticUpdatesEnabled: true,
            AdminRequiredForChanges: false,
            HasAdministrativeAccess: true,
            ChangeApplied: false,
            AvailableUpdates: Array.Empty<string>(),
            StatusDetails: "Auto-update actif (simulation)",
            ScanDetails: "Scan simulé: RAS",
            Recommendation: "Continue de dormir, rien à patcher.",
            ConflictDetected: false);

        var service = new UpdateService(new StubAutomaticUpdateDataSource(_ => snapshot));

        var result = await service.ManageAutomaticUpdatesAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Mises à jour automatiques: activées", result.Message);
        Assert.Contains("Recommandation", result.Message);
    }

    [Fact]
    public async Task ManageAutomaticUpdates_WarnsWhenAdminMissingForToggle()
    {
        var snapshot = new AutomaticUpdateSnapshot(
            Supported: true,
            AutomaticUpdatesEnabled: false,
            AdminRequiredForChanges: true,
            HasAdministrativeAccess: false,
            ChangeApplied: false,
            AvailableUpdates: Array.Empty<string>(),
            StatusDetails: "Politique manuelle en place",
            ScanDetails: "Scan simulé",
            Recommendation: "Demande d'activation à valider par un admin.",
            ConflictDetected: false);

        var service = new UpdateService(new StubAutomaticUpdateDataSource(_ => snapshot));

        var result = await service.ManageAutomaticUpdatesAsync(new AutoUpdateUserIntent(AutoUpdateToggle.Enable), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Pas de droits admin", result.Message);
    }

    [Fact]
    public async Task ManageAutomaticUpdates_ListsUpdates_AndSuggestsLaunch()
    {
        var updates = new List<string>
        {
            "KB5000001 (sécurité cumulatif)",
            "Pilote audio 1.2.3"
        };

        var snapshot = new AutomaticUpdateSnapshot(
            Supported: true,
            AutomaticUpdatesEnabled: true,
            AdminRequiredForChanges: true,
            HasAdministrativeAccess: true,
            ChangeApplied: false,
            AvailableUpdates: updates,
            StatusDetails: "Auto-update actif",
            ScanDetails: "Scan simulé: 2 mises à jour",
            Recommendation: "Planifier l'installation quand tu t'ennuies.",
            ConflictDetected: false);

        var service = new UpdateService(new StubAutomaticUpdateDataSource(_ => snapshot));

        var result = await service.ManageAutomaticUpdatesAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("KB5000001", result.Message);
        Assert.Contains("Pilote audio", result.Message);
        Assert.Contains("Lancer les mises à jour", result.Message);
    }

    private sealed class StubAutomaticUpdateDataSource : IAutomaticUpdateDataSource
    {
        private readonly Func<AutoUpdateUserIntent, AutomaticUpdateSnapshot> _factory;

        public StubAutomaticUpdateDataSource(Func<AutoUpdateUserIntent, AutomaticUpdateSnapshot> factory)
            => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public Task<AutomaticUpdateSnapshot> CaptureAsync(AutoUpdateUserIntent intent, CancellationToken ct = default)
            => Task.FromResult(_factory(intent));
    }
}
