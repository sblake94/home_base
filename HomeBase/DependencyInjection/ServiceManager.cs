
using System;
using System.Runtime.InteropServices;
using HomeBase.Services;
using HomeBase.Services.ChatService;
using HomeBase.Services.DocumentService;
using HomeBase.SharedLib.Logging;
using HomeBase.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HomeBase.DependencyInjection;

public static class ServiceManager
{
	public static IServiceProvider ServiceProvider { get; private set; } = null!;

	public static void ConfigureServices()
	{
		var services = new ServiceCollection();

        RegisterServices(services);

		services.AddSingleton<ChatViewModel>();
		services.AddTransient<TextEditorViewModel>();
		services.AddSingleton<MainWindowViewModel>();

		ServiceProvider = services.BuildServiceProvider(validateScopes: true);
	}

	public static T GetRequiredService<T>() where T : notnull
	{
		if (ServiceProvider is null)
		{
			throw new InvalidOperationException("ServiceManager is not configured. Call ConfigureServices() before resolving services.");
		}

		return ServiceProvider.GetRequiredService<T>();
	}

	private static void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<CoreGrpcChannelFactory>();
		services.AddSingleton<CoreChatService>();
		services.AddSingleton<IChatService>(sp => sp.GetRequiredService<CoreChatService>());
		services.AddSingleton<DocumentService>();
		services.AddSingleton<IDocumentService>(sp => sp.GetRequiredService<DocumentService>());
		services.AddSingleton<IBackendStatusService>(sp => sp.GetRequiredService<CoreChatService>());

        services.AddSingleton<ILoggerFactory, LoggerFactory>();
	}
}

