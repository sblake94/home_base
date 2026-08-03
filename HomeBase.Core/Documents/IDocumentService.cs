using System.Threading;
using System.Threading.Tasks;

namespace HomeBase.Core.Documents;

public interface IDocumentService
{
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAsync(string path, string content, CancellationToken cancellationToken = default);
}