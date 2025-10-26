using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    public static class BrowserCleaningServiceExtensions
    {
        public static Task<string> AnalyzeAndCleanAsync(this BrowserCleaningService _)
            => Task.FromResult("[Browsers] Analyse & nettoyage (résumé).");
    }
}
