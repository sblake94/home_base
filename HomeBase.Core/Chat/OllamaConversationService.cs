using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.Core.Tools;
using HomeBase.SharedLib.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using OllamaChat = OllamaSharp.Chat;

namespace HomeBase.Core.Chat;

public sealed class OllamaConversationService : IConversationService
{
    private readonly CoreSettings _settings;
    private readonly IConversationStore _store;
    private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();
    private readonly IDocumentService _fileDocumentService;
    private readonly ILogger<OllamaConversationService> _log;

    private readonly ILoggerFactory _loggerFactory;
    public OllamaConversationService(
        IDocumentService fileDocumentService, 
        CoreSettings settings, 
        IConversationStore store,
        ILoggerFactory loggerFactory)
    {
        _fileDocumentService = fileDocumentService;
        _settings = settings;
        _store = store;
        _loggerFactory = loggerFactory;
        _log = _loggerFactory.CreateLogger<OllamaConversationService>();
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
        
        state.Tools.Clear();
        state.Tools.AddRange(
        [
            new GetWeatherTool(),
            new ListDocumentNamesTool(),
            new ReadDocumentTool()
        ]);
        
        state.Chat.OnToolCall += HandleToolCall;
        state.Chat.OnToolResult += HandleToolResult;

        await state.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        var messageId = Guid.NewGuid().ToString("N");
        _store.RecordUserMessage(conversationId, content);
        _store.BeginAssistantMessage(conversationId, messageId);

        var accumulated = new StringBuilder();
        var cancelled = false;
        Exception? failure = null;

        foreach(var tool in state.Tools)
        {
            if (tool is not OllamaSharp.Models.Chat.Tool chatTool)
            {
                _log.LogWarning($"Tool {tool.GetType().Name} is not compatible with OllamaSharp.Models.Chat.Tool and will be skipped.");
                continue;
            }

            _log.LogInfo($"{chatTool.Function?.Name ?? "UnKnown"}\t\t[purple]{chatTool.Function?.Description ?? "No description"}[/]");
        }


        var enumerator = state.Chat.SendAsync(content, state.Tools, null, null, cancellationToken).GetAsyncEnumerator(cancellationToken);
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
            state.Chat.OnToolCall -= HandleToolCall;
            state.Chat.OnToolResult -= HandleToolResult;
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

    private void HandleToolCall(object? sender, Message.ToolCall e)
    {
        
    }

    private void HandleToolResult(object? sender, ToolResult e)
    {
        _log.LogInfo($"Tool result received: {e.Tool.GetType().Name} - {e.Result}");
    }

    private ConversationState CreateConversation()
    {
        var settings = _settings.GetOllamaSettings();
        var client = new OllamaApiClient(settings.Endpoint, settings.Model);
        _log.LogInfo($"Created new Ollama conversation with endpoint {settings.Endpoint} and model {settings.Model}");
        _log.LogInfo($"System prompt: {settings.SystemPrompt}");
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
        public List<Tool> Tools { get; } = new();
    }
}
