using System;
using System.IO;
using System.Text.Json;
using HomeBase.Core.Settings;
using HomeBase.SharedLib.Logging;
using Moq;
using Xunit;

namespace HomeBase.Core.Tests.Settings;

public class CoreSettingsTests : IDisposable
{
    private readonly string _tempDirectory;

    public CoreSettingsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-settings-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatesDefaultSettingsFileWhenMissing()
    {
        var settingsPath = Path.Combine(_tempDirectory, "settings.json");
        var legacyPath = Path.Combine(_tempDirectory, "legacy", "local_settings.json");

        var settings = new CoreSettings(new Mock<ICustomLoggerFactory>().Object, settingsPath, legacyPath);

        Assert.True(File.Exists(settingsPath));
        var ollamaSettings = settings.GetOllamaSettings();
        Assert.Equal(OllamaSettings.Default.Endpoint, ollamaSettings.Endpoint);
        Assert.Equal(OllamaSettings.Default.Model, ollamaSettings.Model);
        Assert.Equal(OllamaSettings.Default.SystemPrompt, ollamaSettings.SystemPrompt);
    }

    [Fact]
    public void ReadsPreviouslyWrittenSettings()
    {
        var settingsPath = Path.Combine(_tempDirectory, "settings.json");
        var legacyPath = Path.Combine(_tempDirectory, "legacy", "local_settings.json");
        Directory.CreateDirectory(_tempDirectory);
        var expected = new OllamaSettings("http://example.local:11434", "custom-model", "Custom prompt");
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(expected));

        var settings = new CoreSettings(new Mock<ICustomLoggerFactory>().Object, settingsPath, legacyPath);
        var actual = settings.GetOllamaSettings();

        Assert.Equal(expected, actual);
    }
}
