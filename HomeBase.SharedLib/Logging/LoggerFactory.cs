using Microsoft.Extensions.Logging;

namespace HomeBase.SharedLib.Logging;

public interface ICustomLoggerFactory : ILoggerFactory
{
    public ICustomLogger<T> CreateLogger<T, TLogger>() where TLogger : class, ICustomLogger<T>;
}

public class CustomLoggerFactory : ICustomLoggerFactory
{
    private readonly string _logFilePath;
    private readonly string _source;

    public CustomLoggerFactory(string logFilePath, string source)
    {
        _logFilePath = logFilePath;
        _source = source;
    }

    public ICustomLogger<T> CreateLogger<T, TLogger>() where TLogger : class, ICustomLogger<T>
    {
        var loggerGenericType = typeof(FileLogger<>).MakeGenericType(typeof(T));
        return (ICustomLogger<T>)Activator.CreateInstance(loggerGenericType, _logFilePath, _source)!;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var loggerType = Type.GetType(categoryName) ?? typeof(object);
        var loggerGenericType = typeof(FileLogger<>).MakeGenericType(loggerType);
        return (ILogger)Activator.CreateInstance(loggerGenericType, _logFilePath, _source)!;
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // No-op
    }

    public void Dispose()
    {
        // No-op
    }
}