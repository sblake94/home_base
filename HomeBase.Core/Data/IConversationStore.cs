namespace HomeBase.Core.Data;

public interface IConversationStore
{
    void RecordUserMessage(string conversationId, string content);

    void BeginAssistantMessage(string conversationId, string messageId);

    void MarkCompleted(string messageId, string content);

    void MarkIncomplete(string messageId, string content);

    void MarkFailed(string messageId, string content);

    StoredMessage? GetMessage(string messageId);
}

public sealed record StoredMessage(string Id, string ConversationId, string Role, string Content, string Status);
