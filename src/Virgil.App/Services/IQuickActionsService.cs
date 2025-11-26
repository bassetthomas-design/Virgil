using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public interface IQuickActionsService
    {
        Task ExecuteAsync(string actionId, CancellationToken cancellationToken = default);
    }
}
