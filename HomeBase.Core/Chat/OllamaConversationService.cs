using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomeBase.Core.Data;
using HomeBase.Core.Settings;
using OllamaSharp;
using OllamaChat = OllamaSharp.Chat;

namespace HomeBase.Core.Chat;

public sealed class OllamaConversationService : IConversationService
{
    private readonly CoreSettings _settings;
    private readonly IConversationStore _store;
    private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();

    public OllamaConversationService(CoreSettings settings, IConversationStore store)
    {
        _settings = settings;
        _store = store;
    }

    public async IAsyncEnumerable<ChatStreamEvent> SendMessageAsync(
        string conversationId,
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            yield return new ChatFailed("invalid_conversation", "A conversation ID is required.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            yield return new ChatFailed("invalid_message", "Message content is required.");
            yield break;
        }

        var state = _conversations.GetOrAdd(conversationId, _ => CreateConversation());
        await state.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        var messageId = Guid.NewGuid().ToString("N");
        _store.RecordUserMessage(conversationId, content);
        _store.BeginAssistantMessage(conversationId, messageId);

        var accumulated = new StringBuilder();
        var cancelled = false;
        Exception? failure = null;

        var enumerator = state.Chat.SendAsync(content).GetAsyncEnumerator(cancellationToken);
        try
        {
            yield return new AssistantStarted(messageId);

            while (true)
            {
                string? token = null;
                var hasNext = false;

                // Isolated from the yields below: an iterator cannot yield inside a try/catch (CS1626).
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    if (hasNext)
                    {
                        token = enumerator.Current;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (cancelled || failure is not null || !hasNext)
                {
                    break;
                }

                accumulated.Append(token);
                yield return new AssistantToken(token!);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            state.SendLock.Release();
        }

        if (cancelled)
        {
            _store.MarkIncomplete(messageId, accumulated.ToString());
            yield break;
        }

        if (failure is not null)
        {
            _store.MarkFailed(messageId, accumulated.ToString());
            yield return new ChatFailed("ollama_unreachable", "Unable to reach the Ollama backend.");
            yield break;
        }

        _store.MarkCompleted(messageId, accumulated.ToString());
        yield return new AssistantCompleted(messageId);
    }

    public ValueTask<(bool IsReady, string Message)> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _settings.GetOllamaSettings();
            if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _))
            {
                return ValueTask.FromResult((false, "The configured Ollama endpoint is invalid."));
            }

            if (string.IsNullOrWhiteSpace(settings.Model))
            {
                return ValueTask.FromResult((false, "An Ollama model must be configured."));
            }

            return ValueTask.FromResult((true, "Core service is ready."));
        }
        catch (Exception)
        {
            return ValueTask.FromResult((false, "Core settings could not be loaded."));
        }
    }

    private ConversationState CreateConversation()
    {
        var settings = _settings.GetOllamaSettings();
        var client = new OllamaApiClient(settings.Endpoint, settings.Model);
        return new ConversationState(new OllamaChat(client, settings.SystemPrompt));
    }

    private sealed class ConversationState
    {
        public ConversationState(OllamaChat chat)
        {
            Chat = chat;
        }

        public OllamaChat Chat { get; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}
