namespace HomeBase.Core.Documents;

public sealed class DocumentServiceException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
}