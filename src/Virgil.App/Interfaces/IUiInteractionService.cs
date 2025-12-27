using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Models;

namespace Virgil.App.Interfaces
{
    public interface IUiInteractionService
    {
        Task<ActionResult> OpenSettingsAsync(CancellationToken ct);
        Task<ActionResult> ShowHudAsync(CancellationToken ct);
        Task<ActionResult> HideHudAsync(CancellationToken ct);
    }
}
