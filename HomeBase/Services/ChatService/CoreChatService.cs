using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HomeBase.Contracts.Chat.V1;

namespace HomeBase.Services.ChatService;

public sealed class CoreChatService : IChatService, IBackendStatusService
{
    private readonly ChatApi.ChatApiClient _client;
    private readonly string _conversationId = Guid.NewGuid().ToString("N");

    public CoreChatService(CoreGrpcChannelFactory channelFactory)
    {
        _client = new ChatApi.ChatApiClient(channelFactory.CreateChannel());
    }

    public async IAsyncEnumerable<string> SubmitUserMessageAsync(
        string newMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var call = _client.SendMessage(new SendMessageRequest
        {
            ConversationId = _conversationId,
            RequestId = Guid.NewGuid().ToString("N"),
            Content = newMessage
        }, cancellationToken: cancellationToken);

        while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            var streamEvent = call.ResponseStream.Current;
            switch (streamEvent.PayloadCase)
            {
                case ChatEvent.PayloadOneofCase.AssistantToken:
                    yield return streamEvent.AssistantToken.Text;
                    break;
                case ChatEvent.PayloadOneofCase.Error:
                    throw new CoreChatException(streamEvent.Error.Code, streamEvent.Error.Message);
            }
        }
    }

    public async Task<(bool IsReady, string Message)> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetStatusAsync(new GetStatusRequest(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return (response.IsReady, response.Message);
        }
        catch (Exception exception)
        {
            return (false, $"Unable to reach the HomeBase Core service: {exception.Message}");
        }
    }
}

public sealed class CoreChatException : Exception
{
    public CoreChatException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
