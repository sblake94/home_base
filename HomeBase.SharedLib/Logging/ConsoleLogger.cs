using Microsoft.Extensions.Logging;

namespace HomeBase.SharedLib.Logging;

public class ConsoleLogger<T> : ICustomLogger<T>
{
    private const ConsoleColor _infoColor = ConsoleColor.Cyan;
    private const ConsoleColor _warningColor = ConsoleColor.Yellow;
    private const ConsoleColor _errorColor = ConsoleColor.Red;
    
    
    public void LogInfo(string message) => WriteColoredMessage(LogLevel.Information, message);

    public void LogWarning(string message) => WriteColoredMessage(LogLevel.Warning, message);

    public void LogError(string message) => WriteColoredMessage(LogLevel.Error, message);

    private static void WriteColoredMessage(LogLevel level, string message, bool endLine = true)
    {
        var msgColor = level switch
        {
            LogLevel.Information => _infoColor,
            LogLevel.Warning => _warningColor,
            LogLevel.Error => _errorColor,
            _ => ConsoleColor.White,
        };

        // Level
        Console.ForegroundColor = ConsoleColor.Black;
        Console.BackgroundColor = msgColor;
        Console.Write($" {level.ToString().ToUpper()} ");
        Console.ResetColor();

        // Type
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($" - {typeof(T).Name}: ");
        Console.ResetColor();

        // Message
        Console.ForegroundColor = msgColor;
        if (endLine)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Write(message);
        }
        Console.ResetColor();
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Information:
                LogInfo(formatter(state, exception));
                break;
            case LogLevel.Warning:
                LogWarning(formatter(state, exception));
                break;
            case LogLevel.Error:
                LogError(formatter(state, exception));
                break;
            default:
                throw new NotImplementedException($"Log level {logLevel} is not implemented in ConsoleLogger.");
        }
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Information => true,
        LogLevel.Warning => true,
        LogLevel.Error => true,
        _ => false,
    };

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
}