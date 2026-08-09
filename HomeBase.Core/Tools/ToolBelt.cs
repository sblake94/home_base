using HomeBase.Core.Documents;
using HomeBase.SharedLib.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

namespace HomeBase.Core.Tools;

public class ToolBelt
{
    public static IDocumentService? DocumentService;

    /// <summary>
	/// Get the current weather for a city
	/// </summary>
	/// <param name="city">Name of the city</param>
	/// <param name="unit">Temperature unit for the weather</param>
	[OllamaTool]
	public static string GetWeather(string city, Unit unit = Unit.Celsius) => $"It's cold at only 6° {unit} in {city}.";

    public enum Unit
    {
        Celsius,
        Fahrenheit
    }


    /// <summary>
    /// Lists the names of all the documents available in the document service.
    /// </summary>
    /// <returns>A list of document names.</returns>
    [OllamaTool]
    public static object ListDocumentNames()
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
    [OllamaTool]
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