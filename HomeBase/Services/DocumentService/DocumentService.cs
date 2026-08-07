using System;
using System.Threading;
using System.Threading.Tasks;
using HomeBase.Contracts.Documents.V1;

namespace HomeBase.Services.DocumentService;

public class DocumentService(CoreGrpcChannelFactory channelFactory) : IDocumentService
{
    private readonly DocumentApi.DocumentApiClient _client = new(channelFactory.CreateChannel());

    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        using var call = _client.OpenDocumentAsync(new OpenDocumentRequest
        {
            Path = path    
        }, cancellationToken: cancellationToken);

        var response = await call.ResponseAsync.ConfigureAwait(false);
        return response.Content;
    }

    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        using var call = _client.SaveDocumentAsync(new SaveDocumentRequest
        {
            Path = path,
            Content = content
        }, cancellationToken: cancellationToken);

        var response = await call.ResponseAsync.ConfigureAwait(false);
        if (!response.Success)
        {
            throw new DocumentServiceException(response.ErrorCode, response.ErrorMessage);
        }
    }
}

public class DocumentServiceException : Exception
{
    public DocumentServiceException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}