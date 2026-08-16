using Microsoft.Extensions.Logging;

namespace HomeBase.SharedLib.Logging;

public interface ICustomLogger<T> : ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}
