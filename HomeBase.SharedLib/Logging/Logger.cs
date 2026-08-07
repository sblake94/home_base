namespace HomeBase.SharedLib.Logging;

public interface ILogger<T>
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}

public class Logger<T> : ILogger<T>
{
    private const ConsoleColor _infoColor = ConsoleColor.Cyan;
    private const ConsoleColor _warningColor = ConsoleColor.Yellow;
    private const ConsoleColor _errorColor = ConsoleColor.Red;
    


    public void LogInfo(string message) => WriteColoredMessage(LogLevel.Info, message);

    public void LogWarning(string message) => WriteColoredMessage(LogLevel.Warning, message);

    public void LogError(string message) => WriteColoredMessage(LogLevel.Error, message);

    private static void WriteColoredMessage(LogLevel level, string message, bool endLine = true)
    {
        var msgColor = level switch
        {
            LogLevel.Info => _infoColor,
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

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}