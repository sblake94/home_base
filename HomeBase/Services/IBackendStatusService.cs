using System.Threading;
using System.Threading.Tasks;

namespace HomeBase.Services;

public interface IBackendStatusService
{
    Task<(bool IsReady, string Message)> GetStatusAsync(CancellationToken cancellationToken = default);
}
