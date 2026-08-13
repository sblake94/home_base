using Microsoft.Extensions.Logging;

namespace HomeBase.SharedLib.Logging;

public class FileLogger<T>(string Path, string Source) : ICustomLogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Information => true,
        LogLevel.Warning => true,
        LogLevel.Error => true,
        _ => false,
    };

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch(logLevel)
        {
            case LogLevel.Information: LogInfo(formatter(state, exception)); break;
            case LogLevel.Warning: LogWarning(formatter(state, exception)); break;
            case LogLevel.Error: LogError(formatter(state, exception)); break;
            default: throw new NotImplementedException($"Log level {logLevel} is not implemented in FileLogger.");
        }
    }

    public void LogError(string message)
    {
        File.AppendAllLines(Path, ["\n" , $"{FormatLog("FAIL", message)}"]);
    }

    public void LogInfo(string message)
    {
        File.AppendAllLines(Path, ["\n" , $"{FormatLog("INFO", message)}"]);
    }

    public void LogWarning(string message)
    {
        File.AppendAllLines(Path, ["\n" , $"{FormatLog("WARN", message)}"]);
    }

    private string FormatLog(string prefix, string message)
    {
        return $"{DateTime.Now}: {prefix} <{Source}.{typeof(T).FullName}> \n{message}";
    }
}