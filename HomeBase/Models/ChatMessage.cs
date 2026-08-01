using System;

namespace HomeBase.Models;

public sealed class ChatMessage(string text, DateTime timestamp, bool isFromUser)
{
    public string Text { get; private set; } = text;
    public DateTime Timestamp { get; private set; } = timestamp;
    public bool IsFromUser { get; private set; } = isFromUser;
}