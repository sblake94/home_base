using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using HomeBase.Commands;
using HomeBase.Services.DocumentService;
using HomeBase.SharedLib.Logging;

namespace HomeBase.ViewModels;

public sealed class TextEditorViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;
    private readonly ICustomLogger<TextEditorViewModel> _logger;
    private string _currentlyLoadedFilePath = string.Empty;

    public TextEditorViewModel(IDocumentService documentService, ICustomLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TextEditorViewModel, FileLogger<TextEditorViewModel>>();
        _documentService = documentService;
        var userName = Environment.UserName;
        _currentlyLoadedFilePath = $"/home/{userName}/HomeBase/Documents/SecretDocument.txt";
        FileExtension = GetFileExtension(_currentlyLoadedFilePath);

        _logger.LogInfo($"TextEditorViewModel initialized. Current user: {userName}, default document path: {_currentlyLoadedFilePath}");

        _document = new TextDocument
        {
            Text = _documentService.ReadAsync(_currentlyLoadedFilePath).GetAwaiter().GetResult()
        };

        _document.TextChanged += (sender, e) =>
        {
            OnPropertyChanged(nameof(Content));
        };
    }
    
    public string Content => _document.Text;

    public ICommand SaveCommand => new RelayCommand(async _ => await SaveDocumentAsync(), _ => true);
    public ICommand OpenCommand => new RelayCommand(async _ => await OpenDocumentAsync(_currentlyLoadedFilePath), _ => true);

    private TextDocument _document;
    public TextDocument Document 
    { 
        get => _document;
        set
        {
            _document = value;
            OnPropertyChanged(nameof(Document));
        }
    }

    public string Path
    {
        get => _currentlyLoadedFilePath;
        set
        {
            if (_currentlyLoadedFilePath != value)
            {
                _currentlyLoadedFilePath = value;
                OnPropertyChanged(nameof(Path));
            }
        }
    }

    public enum FileExtensions
    {
        Txt,
        Cs,
        Json,
        Xml,
        Html,
        Css,
        Js,
        Python,
        Unknown
    }
    private FileExtensions _fileExtension;
    public FileExtensions FileExtension
    {
        get => _fileExtension;
        set
        {
            if (_fileExtension != value)
            {
                _fileExtension = value;
                OnPropertyChanged(nameof(FileExtension));
                OnPropertyChanged(nameof(SyntaxHighlighting));
            }
        }
    }

    public IHighlightingDefinition? SyntaxHighlighting => FileExtension switch
    {
        FileExtensions.Cs => HighlightingManager.Instance.GetDefinition("C#"),
        FileExtensions.Json => HighlightingManager.Instance.GetDefinition("Json"),
        FileExtensions.Xml => HighlightingManager.Instance.GetDefinition("XML"),
        FileExtensions.Html => HighlightingManager.Instance.GetDefinition("HTML"),
        FileExtensions.Css => HighlightingManager.Instance.GetDefinition("CSS"),
        FileExtensions.Js => HighlightingManager.Instance.GetDefinition("JavaScript"),
        FileExtensions.Python => HighlightingManager.Instance.GetDefinition("Python"),
        _ => null
    };

    private static FileExtensions GetFileExtension(string path) =>
        System.IO.Path.GetExtension(path)?.ToLowerInvariant() switch
        {
            ".txt" => FileExtensions.Txt,
            ".cs" => FileExtensions.Cs,
            ".json" => FileExtensions.Json,
            ".xml" => FileExtensions.Xml,
            ".html" => FileExtensions.Html,
            ".css" => FileExtensions.Css,
            ".js" => FileExtensions.Js,
            ".py" => FileExtensions.Python,
            _ => FileExtensions.Unknown
        };
    private async Task SaveDocumentAsync()
    {
        // Implement the logic to save the document here
        try
        {
            await _documentService.WriteAsync(_currentlyLoadedFilePath, _document.Text);
            _logger.LogInfo($"Document saved to {_currentlyLoadedFilePath}");
            FileExtension = GetFileExtension(_currentlyLoadedFilePath);
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(SyntaxHighlighting));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save document: {ex.Message}");
        }
    }

    private async Task OpenDocumentAsync(string path)
    {
        _logger.LogInfo($"OpenDocument method called with path: {path}.");

        try
        {
            var content = await _documentService.ReadAsync(path);
            Document = new TextDocument { Text = content };
            _currentlyLoadedFilePath = path;
            OnPropertyChanged(nameof(Path));
            FileExtension = GetFileExtension(_currentlyLoadedFilePath);

            _logger.LogInfo($"Document opened from {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to open document: {ex.Message}");
        }
    }
}