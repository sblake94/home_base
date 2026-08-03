using System.Collections.Generic;
using System.Threading;

namespace HomeBase.Services;

public interface IChatService
{
    IAsyncEnumerable<string> SubmitUserMessageAsync(string newMessage, CancellationToken cancellationToken = default);
}
