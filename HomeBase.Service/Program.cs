using HomeBase.Core.Chat;
using HomeBase.Core.Data;
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

builder.Services.AddGrpc();
builder.Services.AddSingleton<CoreSettings>();
builder.Services.AddSingleton<IConversationStore, SqliteConversationStore>();
builder.Services.AddSingleton<IConversationService, OllamaConversationService>();


var app = builder.Build();
app.MapGrpcService<ChatGrpcService>();
app.MapGet("/", () => "HomeBase Core is running.");

app.Run();
