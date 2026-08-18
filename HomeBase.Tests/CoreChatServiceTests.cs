
using Microsoft.AspNetCore.Builder;
using HomeBase.Services.ChatService;
using HomeBase.Contracts.Chat.V1;
using HomeBase.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace HomeBase.Tests.Services;
public class CoreChatServiceTests
{
    [Fact]
    public async Task CancellationFlowsToBothCallAndStreamEnumeration()
    {
        await using var server = await ScriptedChatServer.StartAsync([ContractEvent("AssistantToken", "waiting")], holdOpen: true);
        using var cancellationSource = new CancellationTokenSource();
        var collecting = server.CollectAsync("cancel", cancellationSource.Token);
        await server.FirstEventWritten.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        _ = await Record.ExceptionAsync(() => collecting);

        Assert.True(server.CallCancellationObserved.Task.IsCompleted);
    }

    [Fact]
    public async Task ContractErrorsRetainTheirCodeAndMessage()
    {
        await using var server = await ScriptedChatServer.StartAsync([new ChatEvent { Error = new ChatError { Code = "unavailable", Message = "Agent is unavailable" } }]);

        var exception = await Assert.ThrowsAsync<CoreChatException>(() => server.CollectAsync("hello"));

        Assert.Equal("unavailable", exception.Code);
        Assert.Equal("Agent is unavailable", exception.Message);

    }

    private static ChatEvent ContractEvent(string typeName, params object[] arguments)
    {
        var payloadType = typeof(ChatEvent).Assembly.GetType($"HomeBase.Contracts.Chat.V1.{typeName}");
        Assert.NotNull(payloadType);
        var payload = Activator.CreateInstance(payloadType!);
        Assert.NotNull(payload);
        foreach (var (propertyName, value) in GetPayloadProperties(typeName, arguments))
        {
            payloadType!.GetProperty(propertyName)?.SetValue(payload, value);
        }

        var chatEvent = new ChatEvent();
        typeof(ChatEvent).GetProperty(typeName)?.SetValue(chatEvent, payload);
        Assert.Equal(typeName, chatEvent.PayloadCase.ToString());
        return chatEvent;
    }

    private static IEnumerable<(string PropertyName, object Value)> GetPayloadProperties(string typeName, object[] arguments)
    {
        return typeName switch
        {
            "AssistantStarted" or "AssistantCompleted" or "AssistantInterrupted" => [("MessageId", arguments[0])],
            "AssistantToken" => [("Text", arguments[0])],
            "ToolCallStarted" or "ToolCallCompleted" => [("ToolName", arguments[0])],
            _ => throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unsupported test payload.")
        };
    }

    private sealed class ScriptedChatServer : IAsyncDisposable
    {
        private readonly string _runtimeDirectory;
        private readonly string? _previousRuntimeDirectory;
        private readonly WebApplication _application;
        private readonly ScriptState _state;
        private readonly CoreGrpcChannelFactory _channelFactory;
        private readonly CoreChatService _service;

        private ScriptedChatServer(string runtimeDirectory, string? previousRuntimeDirectory, WebApplication application, ScriptState state)
        {
            _runtimeDirectory = runtimeDirectory;
            _previousRuntimeDirectory = previousRuntimeDirectory;
            _application = application;
            _state = state;
            _channelFactory = new CoreGrpcChannelFactory();
            _service = new CoreChatService(_channelFactory);
        }

        public IReadOnlyList<SendMessageRequest> Requests => _state.Requests;
        public TaskCompletionSource<bool> FirstEventWritten => _state.FirstEventWritten;
        public TaskCompletionSource<bool> CallCancellationObserved => _state.CallCancellationObserved;

        public static async Task<ScriptedChatServer> StartAsync(IReadOnlyList<ChatEvent> events, bool holdOpen = false)
        {
            var runtimeDirectory = Path.Combine(Path.GetTempPath(), $"homebase-client-tests-{Guid.NewGuid():N}");
            var socketDirectory = Path.Combine(runtimeDirectory, "homebase");
            Directory.CreateDirectory(socketDirectory);
            var socketPath = Path.Combine(socketDirectory, "core.sock");
            var previousRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runtimeDirectory);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(socketPath, listen => listen.Protocols = HttpProtocols.Http2));
            builder.Services.AddGrpc();
            var state = new ScriptState(events, holdOpen);
            builder.Services.AddSingleton(state);
            var application = builder.Build();
            application.MapGrpcService<ScriptedChatApi>();
            await application.StartAsync();
            return new ScriptedChatServer(runtimeDirectory, previousRuntimeDirectory, application, state);
        }

        public Task<List<object>> SubmitAsync(string message) => CollectAsync(message);

        public async Task<List<object>> CollectAsync(string message, CancellationToken cancellationToken = default)
        {
            var stream = Assert.IsAssignableFrom<IAsyncEnumerable<object>>(_service.SubmitUserMessageAsync(message, cancellationToken));
            var events = new List<object>();
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                events.Add(item);
            }

            return events;
        }

        public async ValueTask DisposeAsync()
        {
            _channelFactory.Dispose();
            await _application.DisposeAsync();
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", _previousRuntimeDirectory);
            if (Directory.Exists(_runtimeDirectory))
            {
                Directory.Delete(_runtimeDirectory, recursive: true);
            }
        }
    }

    private sealed class ScriptState(IReadOnlyList<ChatEvent> events, bool holdOpen)
    {
        public List<SendMessageRequest> Requests { get; } = [];
        public TaskCompletionSource<bool> FirstEventWritten { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CallCancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<ChatEvent> Events { get; } = events;
        public bool HoldOpen { get; } = holdOpen;
    }

    private sealed class ScriptedChatApi(ScriptState state) : ChatApi.ChatApiBase
    {
        public override async Task SendMessage(SendMessageRequest request, IServerStreamWriter<ChatEvent> responseStream, ServerCallContext context)
        {
            state.Requests.Add(request);
            using var registration = context.CancellationToken.Register(() => state.CallCancellationObserved.TrySetResult(true));
            foreach (var chatEvent in state.Events)
            {
                await responseStream.WriteAsync(chatEvent);
                state.FirstEventWritten.TrySetResult(true);
            }

            if (state.HoldOpen)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
            }
        }
    }
}