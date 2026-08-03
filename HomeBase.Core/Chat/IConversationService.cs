using System.Collections.Generic;
using System.Threading;

namespace HomeBase.Core.Chat;

public interface IConversationService
{
    IAsyncEnumerable<ChatStreamEvent> SendMessageAsync(
        string conversationId,
        string content,
        CancellationToken cancellationToken = default);

    ValueTask<(bool IsReady, string Message)> GetStatusAsync(CancellationToken cancellationToken = default);
}