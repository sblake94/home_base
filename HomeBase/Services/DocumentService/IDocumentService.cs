using System.Threading;
using System.Threading.Tasks;

namespace HomeBase.Services.DocumentService;

public interface IDocumentService
{
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAsync(string path, string content, CancellationToken cancellationToken = default);
}