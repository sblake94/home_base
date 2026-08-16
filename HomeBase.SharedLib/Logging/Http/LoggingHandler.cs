using HomeBase.SharedLib.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HomeBase.SharedLib.Logging.Http;

public class LoggingHandler : DelegatingHandler
{
    private readonly ICustomLogger<LoggingHandler> _log;

    public LoggingHandler(ICustomLoggerFactory loggerFactory, HttpMessageHandler innerHandler) 
        : base(innerHandler)
    {
        _log = loggerFactory.CreateLogger<LoggingHandler, FileLogger<LoggingHandler>>();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _log.LogInfo($"Request: {request}");
        if (request.Content is not null)
        {
            var bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var rawText = System.Text.Encoding.UTF8.GetString(bodyBytes);
            _log.LogInfo(rawText);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        _log.LogInfo($"Response: {response}");
        if (response.Content is not null)
        {
            var bodyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var rawText = System.Text.Encoding.UTF8.GetString(bodyBytes);
            _log.LogInfo(rawText);
        }

        return response;
    }
}
