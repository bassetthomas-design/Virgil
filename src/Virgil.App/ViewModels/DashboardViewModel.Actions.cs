using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    // Même accessibilité que l'autre partial (internal), sinon CS0262.
    internal partial class DashboardViewModel
    {
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;

            var msg = IsSurveillanceEnabled
                ? "🔍 Surveillance ACTIVÉE. Je garde un œil sur tout."
                : "😴 Surveillance arrêtée. Petite pause…";

            AppendChat(msg);
            Status = msg;
        }

        public async Task RunMaintenanceAsync()
        {
            AppendChat("🛠️ Maintenance complète : démarrage…");
            Status = "Maintenance en cours…";

            // TODO: enchaîner nettoyage intelligent → navigateurs → MAJ globales
            await Task.Delay(300); // placeholder

            AppendChat("✅ Maintenance terminée.");
            Status = "Maintenance terminée.";
        }

        public async Task CleanTempFilesAsync()
        {
            AppendChat("🧹 Nettoyage des temporaires…");
            Status = "Nettoyage temporaires…";

            // TODO: logique de nettoyage TEMP
            await Task.Delay(200); // placeholder

            AppendChat("✅ Temporaires nettoyés.");
            Status = "Temporaires nettoyés.";
        }

        public async Task CleanBrowsersAsync()
        {
            AppendChat("🧼 Nettoyage des navigateurs (caches)…");
            Status = "Nettoyage navigateurs…";

            // TODO: logique de nettoyage navigateurs
            await Task.Delay(200); // placeholder

            AppendChat("✅ Navigateurs nettoyés.");
            Status = "Navigateurs nettoyés.";
        }

        public async Task UpdateAllAsync()
        {
            AppendChat("⬆️ Mises à jour globales (apps/jeux/Windows/drivers/Defender)…");
            Status = "Mises à jour…";

            // TODO: winget + WU + drivers + Defender
            await Task.Delay(300); // placeholder

            AppendChat("✅ Tout est à jour.");
            Status = "Tout est à jour.";
        }

        public async Task RunDefenderScanAsync()
        {
            AppendChat("🛡️ Microsoft Defender : MAJ signatures + scan rapide…");
            Status = "Defender en cours…";

            // TODO: MAJ signatures + scan
            await Task.Delay(200); // placeholder

            AppendChat("✅ Defender OK.");
            Status = "Defender OK.";
        }

        public void OpenConfiguration()
        {
            AppendChat("⚙️ Ouverture de la configuration…");
            Status = "Configuration ouverte.";
            // TODO: ouvrir la fenêtre/onglet de config
        }
    }
}
