using HomeBase.Core.Chat;
using HomeBase.Core.Data;
using HomeBase.Core.Documents;
using HomeBase.Core.Settings;
using HomeBase.Service;
using HomeBase.Service.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var socketPath = CoreSocketPath.Get();
CoreSocketPath.Prepare(socketPath);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
	options.ListenUnixSocket(socketPath, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});


var documentWorkspace =
    Environment.GetEnvironmentVariable("HOMEBASE_DOCUMENT_WORKSPACE")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "HomeBase",
        "Documents");

builder.Services.AddSingleton<HomeBase.SharedLib.Logging.ILoggerFactory, HomeBase.SharedLib.Logging.LoggerFactory>();
builder.Services.AddSingleton<IDocumentService>(
    sp => new FileDocumentService(
        documentWorkspace,
        sp.GetRequiredService<HomeBase.SharedLib.Logging.ILoggerFactory>()));


builder.Services.AddGrpc();
builder.Services.AddSingleton<CoreSettings>();
builder.Services.AddSingleton<IConversationStore, SqliteConversationStore>();
builder.Services.AddSingleton<IConversationService, OllamaConversationService>();


var app = builder.Build();
app.MapGrpcService<ChatGrpcService>();
app.MapGrpcService<DocumentGrpcService>();
app.MapGet("/", () => "HomeBase Core is running.");

app.Run();
