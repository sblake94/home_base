using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace HomeBase.Services.SettingsService;

public class LocalSettingsService
{
    private readonly string _settingsFilePath;
    private readonly Lock _fileLock = new();

    public LocalSettingsService(string? settingsFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
        {
            var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsFilePath = Path.Combine(folderPath, "HomeBase", "local_settings.json");
        }
        else
        {
            _settingsFilePath = settingsFilePath;
        }

        EnsureSettingsFileExists();
    }

    public void WriteSetting(SettingsKey key, string value)
    {
        lock (_fileLock)
        {
            var settings = ReadAllSettings();
            settings[key.ToString()] = value;
            WriteAllSettings(settings);
        }
    }

    public string? ReadSetting(SettingsKey key)
    {
        lock (_fileLock)
        {
            var settings = ReadAllSettings();
            return settings.TryGetValue(key.ToString(), out var value) ? value : null;
        }
    }

    private void EnsureSettingsFileExists()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine($"Ensured directory exists: {directory}");
        }

        if (!File.Exists(_settingsFilePath))
        {
            var defaultSettings = CreateDefaultSettingsFile();
            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Created default settings file at {_settingsFilePath}");
        }

        Console.WriteLine($"Settings file path: {_settingsFilePath}");
    }

    private Dictionary<string, string> CreateDefaultSettingsFile()
    {
        return new Dictionary<string, string>
        {
            { SettingsKey.OllamaEndpoint.ToString(), "http://localhost:11434" },
            { SettingsKey.OllamaModel.ToString(), "llama2" },
            { SettingsKey.OllamaSystemPrompt.ToString(), "You are a helpful assistant." }
        };
    }

    private Dictionary<string, string> ReadAllSettings()
    {
        EnsureSettingsFileExists();

        var json = File.ReadAllText(_settingsFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private void WriteAllSettings(Dictionary<string, string> settings)
    {
        EnsureSettingsFileExists();

        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_settingsFilePath, json);
    }
}