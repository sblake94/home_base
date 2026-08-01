namespace HomeBase.Core.Chat;

public abstract record ChatStreamEvent;

public sealed record AssistantStarted(string MessageId) : ChatStreamEvent;

public sealed record AssistantToken(string Text) : ChatStreamEvent;

public sealed record AssistantCompleted(string MessageId) : ChatStreamEvent;

public sealed record ChatFailed(string Code, string Message) : ChatStreamEvent;