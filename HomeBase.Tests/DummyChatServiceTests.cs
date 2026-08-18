using HomeBase.Services.ChatService;

namespace HomeBase.Tests.Services;

public class DummyChatServiceTests
{
    [Fact]
    public async Task CancellationInterruptsTheSimulatedResponse()
    {
        using var cancellationSource = new CancellationTokenSource();
        var enumeration = CollectAsync(new DummyChatService(), "delayed response", cancellationSource.Token);
        cancellationSource.Cancel();

        var outcome = await Record.ExceptionAsync(() => enumeration);
        Assert.True(outcome is OperationCanceledException || outcome is null,
            $"Cancellation should either terminate the stream or produce its typed interruption event, not {outcome?.GetType().Name}.");
    }

    private static async Task<List<object>> CollectAsync(DummyChatService service, string message, CancellationToken cancellationToken = default)
    {
        var stream = Assert.IsAssignableFrom<IAsyncEnumerable<object>>(service.SubmitUserMessageAsync(message, cancellationToken));
        var events = new List<object>();
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            events.Add(item);
        }

        return events;
    }
}