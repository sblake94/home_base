namespace HomeBase.Core.Documents;

public sealed class FileDocumentService : IDocumentService
{
    public static readonly string InvalidPathErrorCode = "INVALID_PATH";
    public static readonly string PathOutsideWorkspaceErrorCode = "PATH_OUTSIDE_WORKSPACE";


    private readonly string _rootDirectory;

    public FileDocumentService(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DocumentServiceException(InvalidPathErrorCode, "Path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(_rootDirectory, fullPath);

        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}") ||
            Path.IsPathRooted(relativePath))
        {
            throw new DocumentServiceException(PathOutsideWorkspaceErrorCode, "Path is outside the document workspace.");
        }

        return fullPath;
    }

    public Task<string> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(ResolvePath(path), cancellationToken);
    }

    public Task WriteAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        return File.WriteAllTextAsync(ResolvePath(path), content, cancellationToken);
    }
}
