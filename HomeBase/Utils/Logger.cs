using System;
using System.Drawing;

namespace HomeBase.Utils;

public class Logger<T>
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"[{typeof(T).Name}] {message}", Color.LightBlue);
    }

    public void LogError(string message, Exception ex)
    {
        Console.WriteLine($"[{typeof(T).Name}] ERROR: {message} - Exception: {ex}", Color.Red);
    }
}