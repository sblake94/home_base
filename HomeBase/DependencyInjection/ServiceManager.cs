
using System;
using System.Runtime.InteropServices;
using HomeBase.Services;
using HomeBase.Services.ChatService;
using HomeBase.Services.SettingsService;
using HomeBase.Utils;
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
        services.AddSingleton<OllamaChatService>();
        services.AddSingleton<LocalSettingsService>();

        services.AddTransient(typeof(Logger<>));
	}
}
