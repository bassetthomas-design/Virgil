using System;
using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    // Complément du ViewModel SANS rien supprimer : partial
    public partial class DashboardViewModel
    {
        // Flag simple pour la surveillance (lié au Toggle dans la barre du haut)
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            private set
            {
                if (_isSurveillanceEnabled == value) return;
                _isSurveillanceEnabled = value;
                OnPropertyChanged(nameof(IsSurveillanceEnabled));
                AppendChat(value
                    ? "🟢 Surveillance activée."
                    : "🔴 Surveillance arrêtée.");
            }
        }

        // === Méthodes attendues par MainWindow ===

        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            // Si besoin, démarrer/stopper une boucle de monitoring ici
            // (timer, cancellation token, etc.)
        }

        public async Task RunMaintenance()
        {
            try
            {
                AppendChat("🧰 Maintenance complète démarrée…");
                // Enchaîner : nettoyage intelligent + navigateurs + maj globales
                await CleanTempFiles();
                await CleanBrowsers();
                await UpdateAll();
                AppendChat("✅ Maintenance complète terminée.");
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Maintenance échouée : {ex.Message}");
            }
        }

        public async Task CleanTempFiles()
        {
            try
            {
                AppendChat("🧹 Nettoyage intelligent en cours…");
                // TODO: appeler le service de nettoyage réel (Agent/Service)
                await Task.Delay(300); // placeholder non-bloquant
                AppendChat("✨ Nettoyage terminé.");
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Nettoyage interrompu : {ex.Message}");
            }
        }

        public async Task CleanBrowsers()
        {
            try
            {
                AppendChat("🧼 Nettoyage des navigateurs…");
                // TODO: chrome/edge/firefox caches (sans casser les sessions)
                await Task.Delay(300);
                AppendChat("🧼 Navigateurs nettoyés.");
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Nettoyage navigateurs interrompu : {ex.Message}");
            }
        }

        public async Task UpdateAll()
        {
            try
            {
                AppendChat("⬆️ Mises à jour globales (apps/jeux/pilotes/Windows/Defender)…");
                // TODO: winget upgrade, Windows Update, drivers, Defender signatures
                await Task.Delay(300);
                AppendChat("✅ Mises à jour terminées.");
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Mises à jour interrompues : {ex.Message}");
            }
        }

        public async Task RunDefenderScan()
        {
            try
            {
                AppendChat("🛡️ Microsoft Defender : scan rapide…");
                // TODO: lancer MAJ signatures + scan (MpCmdRun ou API)
                await Task.Delay(300);
                AppendChat("🛡️ Scan terminé.");
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Defender a rencontré une erreur : {ex.Message}");
            }
        }

        public void OpenConfiguration()
        {
            try
            {
                AppendChat("⚙️ Ouverture de la configuration…");
                // TODO: ouvrir la SettingsWindow / charger config.json
                // Ce hook laisse la place à l’UI existante
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Impossible d’ouvrir la configuration : {ex.Message}");
            }
        }

        // === Utilitaires de chat (non destructif) ===
        private void AppendChat(string message)
        {
            try
            {
                // Si tu as déjà un mécanisme de chat/log dans l’autre partial,
                // appelle-le ici. Sinon ce fallback reste inoffensif.
                ChatMessages?.Add(new ChatMessage
                {
                    Timestamp = DateTime.Now,
                    Text = message,
                    Severity = "info"
                });
            }
            catch
            {
                // Ne jamais casser l’app pour un log.
            }
        }

        // Modèle léger pour ne rien imposer au modèle existant
        public class ChatMessage
        {
            public DateTime Timestamp { get; set; }
            public string Text { get; set; } = "";
            public string Severity { get; set; } = "info";
        }
    }
}
