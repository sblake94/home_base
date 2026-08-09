using HomeBase.Core.Documents;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

namespace HomeBase.Core.Tools;

public class ListDocumentsTool : Tool
{
    private readonly IDocumentService _documentService;

    public ListDocumentsTool(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Lists the names of all the documents available in the document service.
    /// </summary>
    /// <returns>A list of document names.</returns>
    [OllamaTool]
    public List<string> ListDocuments() => _documentService.ListDocuments();
}