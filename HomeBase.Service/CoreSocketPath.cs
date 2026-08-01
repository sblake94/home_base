using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HomeBase.Service;

internal static class CoreSocketPath
{
    public static string Get()
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            runtimeDirectory = Path.Combine(Path.GetTempPath(), $"homebase-{Environment.UserName}");
        }

        return Path.Combine(runtimeDirectory, "homebase", "core.sock");
    }

    public static void Prepare(string socketPath)
    {
        var directory = Path.GetDirectoryName(socketPath)
            ?? throw new InvalidOperationException("Socket path must include a directory.");

        Directory.CreateDirectory(directory);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // User-only access; the socket has no authentication of its own.
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }
}
