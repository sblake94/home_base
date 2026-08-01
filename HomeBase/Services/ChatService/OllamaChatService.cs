using System;
using System.Threading.Tasks;
using HomeBase.Models;

namespace HomeBase.Services;

public class OllamaChatService : IChatService
{
    private readonly string _model;
    private readonly string _apiKey;

    public OllamaChatService(string model, string apiKey)
    {
        _model = model;
        _apiKey = apiKey;
    }

    public async Task<ChatMessage> SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        // Simulate a delay for sending the message
        await Task.Delay(500);

        // Here you would typically send the message to the Ollama API and get a response
        // For demonstration, we'll just echo the message back with a prefix
        var reply = new ChatMessage("Ollama Reply: " + message, DateTime.Now, false);

        return reply;
    }
}