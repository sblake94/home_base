using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace HomeBase.Services;

public sealed class CoreGrpcChannelFactory : IDisposable
{
    private readonly GrpcChannel _channel;

    public CoreGrpcChannelFactory()
    {
        var socketPath = GetSocketPath();
        var socketsHandler = new SocketsHttpHandler
        {
            ConnectCallback = (_, cancellationToken) => ConnectAsync(socketPath, cancellationToken)
        };

        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = socketsHandler
        });
    }

    public GrpcChannel CreateChannel() => _channel;

    public void Dispose()
    {
        _channel.Dispose();
    }

    private static string GetSocketPath()
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            runtimeDirectory = Path.Combine(Path.GetTempPath(), $"homebase-{Environment.UserName}");
        }

        return Path.Combine(runtimeDirectory, "homebase", "core.sock");
    }

    private static async ValueTask<Stream> ConnectAsync(string socketPath, CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}