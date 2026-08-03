using Grpc.Core;
using HomeBase.Contracts.Documents.V1;
using HomeBase.Core.Documents;

namespace HomeBase.Service.Services;

public sealed class DocumentGrpcService(
    IDocumentService documentService,
    ILogger<DocumentGrpcService> logger)
    : DocumentApi.DocumentApiBase
{
    public override async Task<OpenDocumentResponse> OpenDocument(
        OpenDocumentRequest request,
        ServerCallContext context)
    {
        try
        {
            var content = await documentService
                .ReadAsync(request.Path, context.CancellationToken);

            return new OpenDocumentResponse
            {
                Success = true,
                Content = content
            };
        }
        catch (DocumentServiceException exception)
        {
            logger.LogWarning(
                "Unable to open document at path {Path}: {Message}",
                request.Path,
                exception.Message);

            return new OpenDocumentResponse
            {
                Success = false,
                ErrorCode = exception.ErrorCode,
                ErrorMessage = exception.Message
            };
        }
        catch (FileNotFoundException exception)
        {
            return new OpenDocumentResponse
            {
                Success = false,
                ErrorCode = "NOT_FOUND",
                ErrorMessage = exception.Message
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    public override async Task<SaveDocumentResponse> SaveDocument(
        SaveDocumentRequest request,
        ServerCallContext context)
    {
        try
        {
            await documentService.WriteAsync(
                request.Path,
                request.Content,
                context.CancellationToken);

            return new SaveDocumentResponse
            {
                Success = true
            };
        }
        catch (DocumentServiceException exception)
        {
            logger.LogWarning(
                "Unable to save document at path {Path}: {Message}",
                request.Path,
                exception.Message);

            return new SaveDocumentResponse
            {
                Success = false,
                ErrorCode = exception.ErrorCode,
                ErrorMessage = exception.Message
            };
        }
        catch (UnauthorizedAccessException exception)
        {
            return new SaveDocumentResponse
            {
                Success = false,
                ErrorCode = "ACCESS_DENIED",
                ErrorMessage = exception.Message
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }
}