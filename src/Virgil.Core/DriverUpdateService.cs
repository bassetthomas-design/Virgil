using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Mise à jour "best effort" des pilotes via winget.
    /// Pour les GPU/Chipset spécifiques, recommander les outils constructeurs.
    /// </summary>
    public sealed class DriverUpdateService
    {
        public async Task<string> UpgradeDriversAsync()
        {
            // On s'appuie sur winget --all. Les pilotes compatibles apparaîtront ici.
            var app = new ApplicationUpdateService();
            var output = await app.UpgradeAllAsync(includeUnknown: true, silent: true);

            output += "\nNote: pour des pilotes GPU/chipset spécifiques, utilisez également:\n" +
                      "- NVIDIA GeForce Experience / nvidia-smi\n" +
                      "- AMD Adrenalin\n" +
                      "- Intel Driver & Support Assistant\n";
            return output;
        }
    }
}
