using System;
using System.Collections.Generic;
using HomeBase.Services.SettingsService;
using HomeBase.Utils;
using OllamaSharp;

namespace HomeBase.Services.ChatService;

public class OllamaChatService : IChatService
{
    private readonly LocalSettingsService _settingsService;
    private readonly Logger<OllamaChatService> _log;
    private OllamaApiClient _ollamaClient;

    private Chat _chat;
    public Chat Chat => _chat;

    public OllamaChatService(LocalSettingsService localSettingsService, Logger<OllamaChatService> log)
    {
        _settingsService = localSettingsService ?? throw new ArgumentNullException(nameof(localSettingsService));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var endpoint = _settingsService.ReadSetting(SettingsKey.OllamaEndpoint) ?? throw new InvalidOperationException("OllamaEndpoint setting is not configured.");
        var model = _settingsService.ReadSetting(SettingsKey.OllamaModel) ?? throw new InvalidOperationException("OllamaModel setting is not configured.");
        var systemPrompt = _settingsService.ReadSetting(SettingsKey.OllamaSystemPrompt) ?? "You are a helpful assistant.";

        _ollamaClient = new OllamaApiClient(endpoint, model);
        _chat = new Chat(_ollamaClient, systemPrompt);
    }


    public async IAsyncEnumerable<string> SubmitUserMessageAsync(string newMessage)
    {
        string fullResponse = string.Empty;
        _log.LogInformation($"Submitting user message: {newMessage}");
        await foreach(var answerToken in _chat.SendAsync(newMessage))
        {
            yield return answerToken;
            fullResponse += answerToken;
        }

        _log.LogInformation($"Full response received: {fullResponse}");
    }
}