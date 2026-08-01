using System;
using System.Threading.Tasks;
using HomeBase.Models;

namespace HomeBase.Services.ChatService;

public class OllamaChatService : IChatService
{
    public OllamaChatService()
    {
    }

    public async Task<ChatMessage> SendMessage(string message)
    {
        // TODO: Read from the localsettings.json file to get the IP, model, and apiKey values

        throw new NotImplementedException(nameof(SendMessage));
    }
}