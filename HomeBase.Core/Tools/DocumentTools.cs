using System.ComponentModel;
using HomeBase.Core.Documents;

namespace HomeBase.Core.Tools;

public class DocumentTools
{
    public static IDocumentService? DocumentService { get; set; }

    /// <summary>
    /// Lists the names of all the documents available in the document service.
    /// </summary>
    /// <returns>A list of document names.</returns>
    [Description("Lists the names of all the documents available in the document service.")]
    public static IEnumerable<string> ListDocumentNames()
    {
        try
        {
            if (DocumentService == null)
            {
                throw new InvalidOperationException("DocumentService is not initialized.");
            }
            var result = DocumentService!.ListDocuments();
            return result;
        }
        catch (Exception ex)
        {
            // Log the exception and rethrow it
            Console.WriteLine($"Error in ListDocumentNames: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Reads the content of a document by its name.
    /// </summary>
    /// <param name="documentName">The name of the document to read.</param>
    /// <returns>The content of the document as a string.</returns>
    [Description("Reads the content of a document by its name.")]
    public static string ReadDocument(string documentName)
    {
        try
        {
            if (DocumentService == null)
            {
                throw new InvalidOperationException("DocumentService is not initialized.");
            }
            var content = DocumentService!.ReadAsync(documentName).GetAwaiter().GetResult();
            return content;
        }
        catch (Exception ex)
        {
            // Log the exception and rethrow it
            Console.WriteLine($"Error in ReadDocument: {ex.Message}");
            return string.Empty;
        }
    }
}