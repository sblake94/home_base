using System;
using System.Threading.Tasks;
using Grpc.Core;
using HomeBase.Contracts.Chat.V1;
using CoreAssistantCompleted = HomeBase.Core.Chat.AssistantCompleted;
using CoreAssistantStarted = HomeBase.Core.Chat.AssistantStarted;
using CoreAssistantToken = HomeBase.Core.Chat.AssistantToken;
using CoreChatFailed = HomeBase.Core.Chat.ChatFailed;
using ConversationService = HomeBase.Core.Chat.IConversationService;
using StreamEvent = HomeBase.Core.Chat.ChatStreamEvent;

namespace HomeBase.Service.Services;

public sealed class ChatGrpcService : ChatApi.ChatApiBase
{
    private readonly ConversationService _conversationService;
    private readonly ILogger<ChatGrpcService> _logger;

    public ChatGrpcService(ConversationService conversationService, ILogger<ChatGrpcService> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    public override async Task SendMessage(
        SendMessageRequest request,
        IServerStreamWriter<ChatEvent> responseStream,
        ServerCallContext context)
    {
        await foreach (var streamEvent in _conversationService
            .SendMessageAsync(request.ConversationId, request.Content, context.CancellationToken)
            .ConfigureAwait(false))
        {
            await responseStream.WriteAsync(ToContractEvent(streamEvent));
        }
    }

    public override async Task<GetStatusResponse> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        var status = await _conversationService.GetStatusAsync(context.CancellationToken);
        return new GetStatusResponse
        {
            IsReady = status.IsReady,
            Message = status.Message
        };
    }

    private ChatEvent ToContractEvent(StreamEvent streamEvent)
    {
        return streamEvent switch
        {
            CoreAssistantStarted started => new ChatEvent
            {
                AssistantStarted = new HomeBase.Contracts.Chat.V1.AssistantStarted { MessageId = started.MessageId }
            },
            CoreAssistantToken token => new ChatEvent
            {
                AssistantToken = new HomeBase.Contracts.Chat.V1.AssistantToken { Text = token.Text }
            },
            CoreAssistantCompleted completed => new ChatEvent
            {
                AssistantCompleted = new HomeBase.Contracts.Chat.V1.AssistantCompleted { MessageId = completed.MessageId }
            },
            CoreChatFailed failure => new ChatEvent
            {
                Error = new ChatError { Code = failure.Code, Message = failure.Message }
            },
            _ => throw new InvalidOperationException($"Unsupported stream event type: {streamEvent.GetType().Name}")
        };
    }
}