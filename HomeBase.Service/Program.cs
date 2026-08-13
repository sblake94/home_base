using HomeBase.Core.Chat;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.Core.Tools;
using HomeBase.Service;
using HomeBase.Service.Services;
using HomeBase.SharedLib.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var socketPath = CoreSocketPath.Get();
CoreSocketPath.Prepare(socketPath);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
	options.ListenUnixSocket(socketPath, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

var workspacePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HomeBase");

var documentWorkspace =
    Environment.GetEnvironmentVariable("HOMEBASE_DOCUMENT_WORKSPACE")
    ?? Path.Combine(workspacePath, "Documents");

builder.Services.AddSingleton<CoreSettings>(sp => new CoreSettings(sp.GetRequiredService<ICustomLoggerFactory>()));
builder.Services.AddSingleton<ICustomLoggerFactory, CustomLoggerFactory>(sp => new CustomLoggerFactory(Path.Combine(workspacePath, "logs", "service"), nameof(HomeBase.Service)));
builder.Services.AddSingleton<IDocumentService>(sp => new FileDocumentService(
                                                            documentWorkspace,
                                                            sp.GetRequiredService<ICustomLoggerFactory>()));

builder.Services.AddGrpc();
builder.Services.AddSingleton<IConversationStore, SqliteConversationStore>();
builder.Services.AddSingleton<IConversationService, LocalHostConversationService>();
builder.Services.AddSingleton(sp => new CoreSettings(sp.GetRequiredService<ICustomLoggerFactory>()));
builder.Services.AddSingleton(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetRequiredService<CoreSettings>().GetOllamaSettings().Endpoint),
});

var app = builder.Build();
DocumentTools.DocumentService = app.Services.GetRequiredService<IDocumentService>();

app.MapGrpcService<ChatGrpcService>();
app.MapGrpcService<DocumentGrpcService>();
app.MapGet("/", () => "HomeBase Core is running.");

app.Run();
