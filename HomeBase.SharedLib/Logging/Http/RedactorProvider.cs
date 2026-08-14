using HomeBase.SharedLib.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace HomeBase.SharedLib.Logging.Http;

public class RedactorProvider : IRedactorProvider
{
    public Redactor GetRedactor(DataClassificationSet classifications)
    {
        return new StarRedactor(classifications);
    }
}

public class StarRedactor : Redactor
{
    private readonly DataClassificationSet _classifications;
    public StarRedactor(DataClassificationSet classifications) 
    {
        _classifications = classifications ?? throw new ArgumentNullException(nameof(classifications));
    }

    /// <summary>
    /// Gets the length of the redacted result. 
    /// Since we are replacing each character with a '*', the length remains identical to the input.
    /// </summary>
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        return input.Length;
    }

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("The destination buffer is too small to hold the redacted data.", nameof(destination));
        }

        // Fill the target region of the destination span with asterisks
        destination[..source.Length].Fill('*');

        return source.Length;
    }
}

public static class MyTaxonomyClassifications
{
    public static string Name => "MyTaxonomy";

    public static DataClassification Private => new(Name, nameof(Private));
    public static DataClassification Public => new(Name, nameof(Public));
    public static DataClassification Personal => new(Name, nameof(Personal));
}