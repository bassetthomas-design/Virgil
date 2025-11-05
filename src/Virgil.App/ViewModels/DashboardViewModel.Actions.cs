namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel
    {
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            AppendChat(IsSurveillanceEnabled
                ? "Surveillance activée."
                : "Surveillance arrêtée.");
        }

        public void RunMaintenance()
        {
            // TODO: brancher le pipeline Maintenance complète (intelligent + navigateurs + updates + SFC/DISM)
            AppendChat("Maintenance complète : démarrage (TODO: implémentation service).");
        }

        public void CleanTempFiles()
        {
            // TODO: appel agent/nettoyage temp
            AppendChat("Nettoyage des fichiers temporaires (TODO).");
        }

        public void CleanBrowsers()
        {
            // TODO: nettoyages navigateurs multiples
            AppendChat("Nettoyage des navigateurs (TODO).");
        }

        public void UpdateAll()
        {
            // TODO: winget + Store + Windows Update + drivers
            AppendChat("Mises à jour globales (TODO).");
        }

        public void RunDefenderScan()
        {
            // TODO: MAJ signatures + scan rapide
            AppendChat("Microsoft Defender : mise à jour + scan rapide (TODO).");
        }

        public void OpenConfiguration()
        {
            // TODO: ouverture de la fenêtre/onglet de configuration
            AppendChat("Ouverture de la configuration (TODO).");
        }
    }
}
