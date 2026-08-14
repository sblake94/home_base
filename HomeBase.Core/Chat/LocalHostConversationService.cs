using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.Core.Tools;
using HomeBase.SharedLib.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using OllamaSharp;


namespace HomeBase.Core.Chat;

public sealed class LocalHostConversationService : IConversationService
{
    private readonly CoreSettings _settings;
    private readonly IConversationStore _store;
    private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();
    private readonly IDocumentService _fileDocumentService;
    private readonly HttpClient? _httpClient;
    private readonly ICustomLogger<LocalHostConversationService> _log;

    private readonly ICustomLoggerFactory _loggerFactory;
    public LocalHostConversationService(
        IDocumentService fileDocumentService, 
        CoreSettings settings, 
        IConversationStore store,
        ICustomLoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        _fileDocumentService = fileDocumentService;
        _settings = settings;
        _store = store;
        _loggerFactory = loggerFactory;
        _log = _loggerFactory.CreateLogger<LocalHostConversationService, FileLogger<LocalHostConversationService>>();

        _httpClient = serviceProvider.GetService<HttpClient>()
            ?? serviceProvider.GetKeyedService<HttpClient>("OllamaClient");

        if (_httpClient is not null)
        {
            _log.LogInfo($"Using provided HttpClient at {_httpClient.BaseAddress}");
        }
    }

    public async IAsyncEnumerable<ChatStreamEvent> SendMessageAsync(
        string conversationId,
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(conversationId))
        {
            yield return new ChatFailed("invalid_conversation", "The conversation ID cannot be null.");
            yield break;
        }

        if(string.IsNullOrWhiteSpace(content))
        {
            yield return new ChatFailed("invalid_message", "Message content is required.");
            yield break;
        }

        var state = _conversations.GetOrAdd(conversationId, _ => CreateConversation());

        await state.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        var messageId = Guid.NewGuid().ToString();

        var accumulated = new StringBuilder();
        
        _log.LogInfo($"Sending message to conversation {conversationId} with message ID {messageId}: {content}");

        try
        {
            await foreach (var token in state.Agent.RunStreamingAsync(content, state.Session).ConfigureAwait(false))
            {
                yield return new AssistantToken(token.Text);
                accumulated.Append(token.Text);
            }
            
            yield return new AssistantCompleted(messageId);
            _log.LogInfo($"Message {messageId} sent successfully to conversation {conversationId}. Accumulated response: {accumulated}");
        }
        finally
        {
            state.SendLock.Release();
        }
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
        var endpoint = settings.Endpoint;
        var modelName = settings.Model;

        var client = _httpClient ?? throw new ArgumentNullException(nameof(_httpClient), "HttpClient must be provided to create a conversation.");
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(endpoint);
        }

        AIAgent agent = new OllamaApiClient(client, modelName)
            .AsAIAgent(
                name: "Terry",
                instructions: "You are a helpful assistant.", 
                loggerFactory: _loggerFactory,
                tools: 
                [
                    AIFunctionFactory.Create(DocumentTools.ReadDocument, nameof(DocumentTools.ReadDocument), "Reads the content of a document by its name."),
                    AIFunctionFactory.Create(DocumentTools.ListDocumentNames, nameof(DocumentTools.ListDocumentNames), "Lists the names of all the documents available in the document service.")
                ]); 

        return new ConversationState(agent);
    }

    private sealed class ConversationState
    {
        public ConversationState(AIAgent agent)
        {
            Agent = agent;
        }

        public AIAgent Agent { get; }
        public AgentSession Session { get; set; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}
