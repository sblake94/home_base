using HomeBase.SharedLib.Logging;
namespace HomeBase.Core.Documents;

public sealed class FileDocumentService : IDocumentService
{
    private readonly ICustomLogger<FileDocumentService> _log;
    public static readonly string InvalidPathErrorCode = "INVALID_PATH";
    public static readonly string PathNotFullyQualifiedErrorCode = "PATH_NOT_FULLY_QUALIFIED";
    public static readonly string PathOutsideWorkspaceErrorCode = "PATH_OUTSIDE_WORKSPACE";
    private readonly string _rootDirectory;


    public FileDocumentService(string rootDirectory, ICustomLoggerFactory loggerFactory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
        _log = loggerFactory.CreateLogger<FileDocumentService, FileLogger<FileDocumentService>>();
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DocumentServiceException(InvalidPathErrorCode, "Path is required.");
        }

        if(!Path.IsPathFullyQualified(path))
        {
            throw new DocumentServiceException(PathNotFullyQualifiedErrorCode, "Path must be fully qualified.");
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
            _log.LogWarning($"Attempted access to path outside workspace: {fullPath}");
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
        var resolvedPath = ResolveReadPath(path);

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            throw new DocumentServiceException(InvalidPathErrorCode, "Directory does not exist.");
        }
        _log.LogInfo($"Reading document at path: {path}");
        return File.ReadAllTextAsync(resolvedPath, cancellationToken);
    }

    // Relative paths are treated as relative to the root directory, then re-validated through
    // ResolvePath's sandbox checks so a value like "../../etc/passwd" can't escape the root.
    private string ResolveReadPath(string path)
    {
        try
        {
            return ResolvePath(path);
        }
        catch (DocumentServiceException ex) when (ex.ErrorCode == PathNotFullyQualifiedErrorCode)
        {
            return ResolvePath(Path.Combine(_rootDirectory, path));
        }
    }

    public Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        _log.LogInfo($"Writing document at path: {path}");
        var resolvedPath = ResolvePath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return File.WriteAllTextAsync(resolvedPath, content, cancellationToken);
    }

    public List<string> ListDocuments()
    {
        _log.LogInfo($"Listing documents in root directory: {_rootDirectory}");
        var documents = new List<string>();
        foreach (var file in Directory.EnumerateFiles(_rootDirectory, "*", SearchOption.AllDirectories))
        {
            // Get the relative path from the root directory
            var relativePath = Path.GetRelativePath(_rootDirectory, file);
            documents.Add(relativePath);
        }
        return documents;
    }
}
