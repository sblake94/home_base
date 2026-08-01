using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBase.Services.ChatService;

public sealed class DummyChatService : IChatService
{
    public DummyChatService()
    {
    }
    
    public async IAsyncEnumerable<string> SubmitUserMessageAsync(string newMessage)
    {
        if (string.IsNullOrWhiteSpace(newMessage))
        {
            yield return "Please enter a message.";
            yield break;
        }

        // Simulate a delay for sending the message
        await Task.Delay(500);

        // Simulate receiving a reply after sending the message
        var reply = "Echo: " + newMessage;

        // Here you would typically notify the ViewModel or some observer about the new message
        yield return reply;
    }
}
