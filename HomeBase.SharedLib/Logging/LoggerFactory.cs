namespace HomeBase.SharedLib.Logging;

public interface ILoggerFactory
{
    ILogger<T> CreateLogger<T>();
}

public class LoggerFactory : ILoggerFactory
{
    public ILogger<T> CreateLogger<T>()
    {
        return new Logger<T>();
    }
}