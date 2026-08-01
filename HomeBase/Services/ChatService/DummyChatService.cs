using System;
using System.Threading.Tasks;
using HomeBase.Models;

namespace HomeBase.Services.ChatService;

public sealed class DummyChatService : IChatService
{

    public DummyChatService()
    {
    }

    public async Task<ChatMessage> SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        // Simulate a delay for sending the message
        await Task.Delay(500);

        // Simulate receiving a reply after sending the message
        var reply = new ChatMessage("Echo: " + message, DateTime.Now, false);

        // Here you would typically notify the ViewModel or some observer about the new message
        return reply;
    }
}
