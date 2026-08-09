using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using HomeBase.SharedLib.Logging;

namespace HomeBase.Core.Settings;

public sealed class CoreSettings
{
    private readonly ILogger<CoreSettings> _log;
    private const string SettingsFileName = "settings.json";
    private readonly Lock _fileLock = new();
    private readonly string _settingsFilePath;
    private readonly string _legacySettingsFilePath;

    public CoreSettings(ILoggerFactory loggerFactory, string? settingsFilePath = null, string? legacySettingsFilePath = null)
    {
        _log = loggerFactory.CreateLogger<CoreSettings>();
        _settingsFilePath = settingsFilePath ?? GetDefaultSettingsFilePath();
        _legacySettingsFilePath = legacySettingsFilePath ?? GetDefaultLegacySettingsFilePath();
        EnsureSettingsFileExists();
        
        _log.LogInfo($"CoreSettings initialized. Settings file path: {_settingsFilePath}"); 
    }

    public OllamaSettings GetOllamaSettings()
    {
        lock (_fileLock)
        {
            var settings = JsonSerializer.Deserialize<OllamaSettings>(File.ReadAllText(_settingsFilePath));
            return settings ?? OllamaSettings.Default;
        }
    }

    private static string GetDefaultSettingsFilePath()
    {
        var configDirectory = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(configDirectory, "HomeBase", SettingsFileName);
    }

    private static string GetDefaultLegacySettingsFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HomeBase",
            "local_settings.json");
    }

    private void EnsureSettingsFileExists()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath)
            ?? throw new InvalidOperationException("Settings file path must include a directory.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(_settingsFilePath))
        {
            var migrated = TryMigrateFromLegacyUiSettings();
            File.WriteAllText(
                _settingsFilePath,
                JsonSerializer.Serialize(migrated ?? OllamaSettings.Default, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    // The original Avalonia client stored its own key/value settings file before Core existed.
    private OllamaSettings? TryMigrateFromLegacyUiSettings()
    {
        if (!File.Exists(_legacySettingsFilePath))
        {
            return null;
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_legacySettingsFilePath));
            if (legacy is null)
            {
                return null;
            }

            return new OllamaSettings(
                legacy.GetValueOrDefault("OllamaEndpoint", OllamaSettings.Default.Endpoint),
                legacy.GetValueOrDefault("OllamaModel", OllamaSettings.Default.Model),
                legacy.GetValueOrDefault("OllamaSystemPrompt", OllamaSettings.Default.SystemPrompt));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record OllamaSettings(string Endpoint, string Model, string SystemPrompt)
{
    public static OllamaSettings Default { get; } = new(
        "http://localhost:11434",
        "llama2",
        "You are a helpful assistant.");
}