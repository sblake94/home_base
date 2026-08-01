using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HomeBase.Models;
using HomeBase.Services.SettingsService;

namespace HomeBase.Services.ChatService;

public class OllamaChatService : IChatService
{
    private readonly LocalSettingsService _settingsService;

    public OllamaChatService(LocalSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<ChatMessage> SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ChatMessage("Please enter a message.", DateTime.Now, false);
        }

        // Placeholder until Ollama integration is implemented.
        await Task.Delay(100);
        return new ChatMessage($"[ollama stub] {message}", DateTime.Now, false);
    }
}