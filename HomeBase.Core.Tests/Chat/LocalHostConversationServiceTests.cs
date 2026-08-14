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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace HomeBase.Core.Tests.Chat;

public class LocalHostConversationServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public LocalHostConversationServiceTests()
    {
        var loggerFactory = new Mock<ICustomLoggerFactory>().Object;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-conversation-tests-" + Guid.NewGuid().ToString("N"));
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db"));
        var fileDocumentService = new FileDocumentService(Path.Combine(_tempDirectory, "documents"), loggerFactory);
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
        var sut = new LocalHostConversationService(
            new Mock<IDocumentService>().Object,
            new CoreSettings(new Mock<ICustomLoggerFactory>().Object,
                Path.Combine(_tempDirectory, "settings.json"),
                Path.Combine(_tempDirectory, "legacy", "local_settings.json")),
            new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db")),
            new Mock<ICustomLoggerFactory>().Object,
            new ServiceCollection()
                .AddSingleton(new HttpClient())
                .BuildServiceProvider());
        var events = await CollectAsync(sut.SendMessageAsync(string.Empty, "hello"));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_conversation", failed.Code);
    }

    [Fact]
    public async Task ReturnsFailureForEmptyContent()
    {
        var sut = new LocalHostConversationService(
            new Mock<IDocumentService>().Object,
            new CoreSettings(new Mock<ICustomLoggerFactory>().Object,
                Path.Combine(_tempDirectory, "settings.json"),
                Path.Combine(_tempDirectory, "legacy", "local_settings.json")),
            new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db")),
            new Mock<ICustomLoggerFactory>().Object,
            new ServiceCollection()
                .AddSingleton(new HttpClient())
                .BuildServiceProvider());
        var events = await CollectAsync(sut.SendMessageAsync("conversation-1", "   "));

        var failure = Assert.Single(events);
        var failed = Assert.IsType<ChatFailed>(failure);
        Assert.Equal("invalid_message", failed.Code);
    }

    [Fact]
    public async Task SendMessageAsync_SendsHttpRequestWithCorrectToolSchemas()
    {
        var requestBodies = new List<string>();
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                requestBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"model\":\"llama2\",\"created_at\":\"2026-01-01T00:00:00Z\",\"message\":{\"role\":\"assistant\",\"content\":\"hello from test\"},\"done\":false}\n" +
                        "{\"model\":\"llama2\",\"created_at\":\"2026-01-01T00:00:01Z\",\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true}\n",
                        Encoding.UTF8,
                        "application/json")
                });
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var fileDocumentServiceMock = new Mock<IDocumentService>();
        var loggerFactory = new CustomLoggerFactory(
            Path.Combine(_tempDirectory, "logs"), nameof(SendMessageAsync_SendsHttpRequestWithCorrectToolSchemas));
        var storeMock = new Mock<IConversationStore>();
        var settings = new CoreSettings(loggerFactory,
            Path.Combine(_tempDirectory, "settings.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));

        var sut = new LocalHostConversationService(
            fileDocumentServiceMock.Object,
            settings,
            storeMock.Object,
            loggerFactory,
            new ServiceCollection()
                .AddSingleton(httpClient)
                .BuildServiceProvider());

        var events = await CollectAsync(sut.SendMessageAsync("test-conversation", "Hello, assistant!"));

        Assert.DoesNotContain(events, e => e is ChatFailed);
        Assert.NotEmpty(requestBodies);
        Assert.Contains("tools", requestBodies[^1]);
        Assert.Contains("ReadDocument", requestBodies[^1]);
        Assert.Contains("ListDocumentNames", requestBodies[^1]);

        handler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri != null &&
                req.RequestUri.ToString().Contains("api/chat") &&
                req.Content != null),
            ItExpr.IsAny<CancellationToken>());
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

        var requestBodies = new List<string>();
        var responses = new Queue<HttpResponseMessage>();

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
            done = false
        }) + "\n";

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
        }) + "\n";

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

        var loggerFactory = new CustomLoggerFactory(
            Path.Combine(_tempDirectory, "logs"), nameof(ShouldFeedToolOutputBackToAssistant));
        var settings = new CoreSettings(
            loggerFactory,
            Path.Combine(_tempDirectory, "settings-tool-output.json"),
            Path.Combine(_tempDirectory, "legacy", "local_settings.json"));
        var store = new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase-tool-output.db"));
        var fileDocumentService = new FileDocumentService(documentRoot, loggerFactory);
        var sut = new LocalHostConversationService(
            new Mock<IDocumentService>().Object,
            new CoreSettings(loggerFactory,
                Path.Combine(_tempDirectory, "settings.json"),
                Path.Combine(_tempDirectory, "legacy", "local_settings.json")),
            new SqliteConversationStore(Path.Combine(_tempDirectory, "homebase.db")),
            loggerFactory,
            new ServiceCollection()
                .AddSingleton(httpClient)
                .BuildServiceProvider());

        var previousDocumentService = DocumentTools.DocumentService;
        DocumentTools.DocumentService = fileDocumentService;

        // Act
        try
        {
            var events = await CollectAsync(sut.SendMessageAsync("conversation-1", "read the file"));

            // Assert
            Assert.DoesNotContain(events, e => e is ChatFailed);

            httpHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

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
