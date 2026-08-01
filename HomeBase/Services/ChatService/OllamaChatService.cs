using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HomeBase.Models;

namespace HomeBase.Services.ChatService;

public class OllamaChatService : IChatService
{
    readonly static string _folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public OllamaChatService()
    {
        ReadSettings();
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

    private void ReadSettings()
    {
        // Read settings from configuration or environment variables
        // For example, you might read an API key or endpoint URL here  
        var settingsFile = Path.Combine(_folderPath, "HomeBase", "local_settings.json");
        if (!File.Exists(settingsFile))
        {
            return;
        }

        _ = File.ReadAllLines(settingsFile).ToList();
    }
}