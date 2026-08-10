using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HomeBase.Core.Chat;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.Core.Tools;
using HomeBase.SharedLib.Logging;
using Xunit;
using Moq;
using Moq.Protected;

namespace HomeBase.Core.Tests.Chat;

public class OllamaConversationServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly OllamaConversationService _service;

    public OllamaConversationServiceTests()
    {
        var loggerFactory = new LoggerFactory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-conversation-tests-" + Guid.NewGuid().ToString("N"));
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db"));
        var fileDocumentService = new FileDocumentService(Path.Combine(_tempDirectory, "documents"), loggerFactory);
        _service = new OllamaConversationService(fileDocumentService, settings, store, loggerFactory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReturnsFailureForEmptyConversationId()
    {
        var events = await CollectAsync(_service.SendMessageAsync(string.Empty, "hello"));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_conversation", failed.Code);
    }

    [Fact]
    public async Task ReturnsFailureForEmptyContent()
    {
        var events = await CollectAsync(_service.SendMessageAsync("conversation-1", "   "));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_message", failed.Code);
    }

    [Fact]
    // This test should assert that the SendMessageAsync method sends an HTTP request with the correct tool schemas when a message is sent.
    // Use Moq and Xunit
    public async Task SendMessageAsync_SendsHttpRequestWithCorrectToolSchemas()
    {
        // Arrange
        HttpMethod? capturedMethod = null;
        string? capturedUri = null;
        string? capturedBody = null;

        var httpHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .Callback<HttpRequestMessage, System.Threading.CancellationToken>((request, _) =>
            {
                capturedMethod = request.Method;
                capturedUri = request.RequestUri?.AbsoluteUri;
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"model":"llama2","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"ok"},"done":true}
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(httpHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var loggerFactory = new LoggerFactory();
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings-with-http.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase-http.db"));
        var fileDocumentService = new FileDocumentService(Path.Combine(_tempDirectory, "documents-http"), loggerFactory);
        var service = new OllamaConversationService(fileDocumentService, settings, store, loggerFactory, httpClient);

        // Act
        _ = await CollectAsync(service.SendMessageAsync("conversation-1", "hello"));

        // Assert
        httpHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>());

            Assert.Equal(HttpMethod.Post, capturedMethod);
            Assert.NotNull(capturedUri);
            Assert.Contains("/api/chat", capturedUri);

            Assert.NotNull(capturedBody);
            using var json = JsonDocument.Parse(capturedBody!);

        var tools = json.RootElement.GetProperty("tools");
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(2, tools.GetArrayLength());

        var functionNames = tools
            .EnumerateArray()
            .Select(t => t.GetProperty("function").GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("ListDocumentNames", functionNames);
        Assert.Contains("ReadDocument", functionNames);


        var readDocumentTool = tools
            .EnumerateArray()
            .Single(t => t.GetProperty("function").GetProperty("name").GetString() == "ReadDocument");
        var readDocumentParameters = readDocumentTool.GetProperty("function").GetProperty("parameters");
        Assert.Equal("object", readDocumentParameters.GetProperty("type").GetString());
        var readDocumentProperties = readDocumentParameters.GetProperty("properties");
        Assert.True(readDocumentProperties.TryGetProperty("documentName", out _));
    }

    [Fact]
    public async Task ShouldFeedToolOutputBackToAssistant()
    {
        // Arrange
        var documentRoot = Path.Combine(_tempDirectory, "documents-tool-output");
        Directory.CreateDirectory(documentRoot);

        var documentPath = Path.Combine(documentRoot, "tool-output.txt");
        var expectedToolOutput = "TOOL_OUTPUT_MARKER_42";
        await File.WriteAllTextAsync(documentPath, expectedToolOutput);

        var requestBodies = new System.Collections.Generic.List<string>();
        var responses = new System.Collections.Generic.Queue<HttpResponseMessage>();

        var toolCallResponse = JsonSerializer.Serialize(new
        {
            model = "llama2",
            created_at = "2026-01-01T00:00:00Z",
            message = new
            {
                role = "assistant",
                content = "",
                tool_calls = new[]
                {
                    new
                    {
                        function = new
                        {
                            name = "ReadDocument",
                            arguments = new
                            {
                                documentName = documentPath
                            }
                        }
                    }
                }
            },
            done = true
        });

        var finalAssistantResponse = JsonSerializer.Serialize(new
        {
            model = "llama2",
            created_at = "2026-01-01T00:00:01Z",
            message = new
            {
                role = "assistant",
                content = "done"
            },
            done = true
        });

        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(toolCallResponse, Encoding.UTF8, "application/json")
        });

        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(finalAssistantResponse, Encoding.UTF8, "application/json")
        });

        var httpHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .Returns<HttpRequestMessage, System.Threading.CancellationToken>((request, _) =>
            {
                requestBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);

                if (responses.Count == 0)
                {
                    throw new InvalidOperationException("Unexpected extra HTTP request.");
                }

                return Task.FromResult(responses.Dequeue());
            });

        var httpClient = new HttpClient(httpHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var loggerFactory = new LoggerFactory();
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings-tool-output.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase-tool-output.db"));
        var fileDocumentService = new FileDocumentService(documentRoot, loggerFactory);
        var service = new OllamaConversationService(fileDocumentService, settings, store, loggerFactory, httpClient);

        var previousDocumentService = DocumentTools.DocumentService;
        DocumentTools.DocumentService = fileDocumentService;

        // Act
        try
        {
            var events = await CollectAsync(service.SendMessageAsync("conversation-1", "read the file"));

            // Assert
            Assert.DoesNotContain(events, e => e is ChatFailed);

            httpHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>());

            Assert.Equal(2, requestBodies.Count);

            var followUpRequestBody = requestBodies[1];
            Assert.Contains("\"role\":\"tool\"", followUpRequestBody);
            Assert.Contains("ReadDocument", followUpRequestBody);
            Assert.Contains(expectedToolOutput, followUpRequestBody);
        }
        finally
        {
            DocumentTools.DocumentService = previousDocumentService;
        }
    }

    private static async Task<System.Collections.Generic.List<ChatStreamEvent>> CollectAsync(
        System.Collections.Generic.IAsyncEnumerable<ChatStreamEvent> source)
    {
        var results = new System.Collections.Generic.List<ChatStreamEvent>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }
}
