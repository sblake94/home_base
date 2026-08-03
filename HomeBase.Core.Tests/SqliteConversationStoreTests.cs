using System;
using System.IO;
using HomeBase.Core.Data;
using Xunit;

namespace HomeBase.Core.Tests;

public class SqliteConversationStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _databasePath;

    public SqliteConversationStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-store-tests-" + Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_tempDirectory, "homebase.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void RecordsUserMessageAndAssistantLifecycle()
    {
        var store = new SqliteConversationStore(_databasePath);
        const string conversationId = "conversation-1";
        const string messageId = "message-1";

        store.RecordUserMessage(conversationId, "Hello there");
        store.BeginAssistantMessage(conversationId, messageId);

        var pending = store.GetMessage(messageId);
        Assert.NotNull(pending);
        Assert.Equal("Pending", pending!.Status);
        Assert.Equal("assistant", pending.Role);

        store.MarkCompleted(messageId, "Hi! How can I help?");

        var completed = store.GetMessage(messageId);
        Assert.NotNull(completed);
        Assert.Equal("Completed", completed!.Status);
        Assert.Equal("Hi! How can I help?", completed.Content);
    }

    [Fact]
    public void MarksIncompleteOnCancellation()
    {
        var store = new SqliteConversationStore(_databasePath);
        const string conversationId = "conversation-2";
        const string messageId = "message-2";

        store.RecordUserMessage(conversationId, "Tell me a story");
        store.BeginAssistantMessage(conversationId, messageId);
        store.MarkIncomplete(messageId, "Once upon a ti");

        var message = store.GetMessage(messageId);
        Assert.NotNull(message);
        Assert.Equal("Incomplete", message!.Status);
        Assert.Equal("Once upon a ti", message.Content);
    }

    [Fact]
    public void MarksFailedOnBackendError()
    {
        var store = new SqliteConversationStore(_databasePath);
        const string conversationId = "conversation-3";
        const string messageId = "message-3";

        store.RecordUserMessage(conversationId, "Ping");
        store.BeginAssistantMessage(conversationId, messageId);
        store.MarkFailed(messageId, string.Empty);

        var message = store.GetMessage(messageId);
        Assert.NotNull(message);
        Assert.Equal("Failed", message!.Status);
    }
}
