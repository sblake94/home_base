using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HomeBase.Core.Chat;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.SharedLib.Logging;
using Xunit;

namespace HomeBase.Core.Tests.Chat;

public class OllamaConversationServiceValidationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly OllamaConversationService _service;

    public OllamaConversationServiceValidationTests()
    {
        var loggerFactory = new LoggerFactory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-conversation-tests-" + Guid.NewGuid().ToString("N"));
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db"));
        var fileDocumentService = new FileDocumentService(Path.Combine(_tempDirectory, "documents"), loggerFactory);
        _service = new OllamaConversationService(fileDocumentService, settings, store, loggerFactory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReturnsFailureForEmptyConversationId()
    {
        var events = await CollectAsync(_service.SendMessageAsync(string.Empty, "hello"));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_conversation", failed.Code);
    }

    [Fact]
    public async Task ReturnsFailureForEmptyContent()
    {
        var events = await CollectAsync(_service.SendMessageAsync("conversation-1", "   "));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_message", failed.Code);
    }

    private static async Task<System.Collections.Generic.List<ChatStreamEvent>> CollectAsync(
        System.Collections.Generic.IAsyncEnumerable<ChatStreamEvent> source)
    {
        var results = new System.Collections.Generic.List<ChatStreamEvent>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }
}
