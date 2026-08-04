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

        if(!Path.IsPathFullyQualified(path))
        {
            throw new DocumentServiceException(InvalidPathErrorCode, "Path must be fully qualified.");
        }

        // Ensure both the requested path and root are fully resolved lexically
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetFullPath(_rootDirectory); 
        var relativePath = Path.GetRelativePath(rootPath, fullPath);

        // 1. Lexical Sandbox Check (Prevents traditional ".." traversal)
        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}") ||
            Path.IsPathRooted(relativePath))
        {
            throw new DocumentServiceException(PathOutsideWorkspaceErrorCode, "Path is outside the document workspace.");
        }

        // 2. Physical Sandbox Check (Prevents Symlink / Reparse Point bypass)
        var currentPath = fullPath;
        
        while (!string.IsNullOrEmpty(currentPath))
        {
            // Stop checking once we've safely walked up to the trusted root directory.
            // Using GetRelativePath == "." is a safe, cross-platform way to check path equality 
            // without worrying about case-sensitivity (Windows vs Linux) or trailing slashes.
            if (Path.GetRelativePath(rootPath, currentPath) == ".")
            {
                break;
            }

            // Check if the file/directory exists before querying attributes.
            // (This allows creating NEW files, as it will just skip up to the parent directory)
            if (File.Exists(currentPath) || Directory.Exists(currentPath))
            {
                var attributes = File.GetAttributes(currentPath);
                
                // In .NET, Unix symlinks and Windows Junctions/Symlinks are all flagged as ReparsePoints
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    throw new DocumentServiceException(
                        PathOutsideWorkspaceErrorCode, 
                        "Symlinks and reparse points are not permitted within the workspace.");
                }
            }

            // Move up to the parent directory segment
            currentPath = Path.GetDirectoryName(currentPath);
        }

        return fullPath;
    }

    public Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(ResolvePath(path), cancellationToken);
    }

    public Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return File.WriteAllTextAsync(resolvedPath, content, cancellationToken);
    }
}
