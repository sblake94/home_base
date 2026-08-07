using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AvaloniaEdit.Document;
using HomeBase.Commands;
using HomeBase.Services.DocumentService;
using HomeBase.SharedLib.Logging;

namespace HomeBase.ViewModels;

public sealed class TextEditorViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<TextEditorViewModel> _logger;
    private string _currentlyLoadedFilePath = string.Empty;

    public TextEditorViewModel(IDocumentService documentService, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TextEditorViewModel>();
        _documentService = documentService;
        var userName = Environment.UserName;
        _currentlyLoadedFilePath = $"/home/{userName}/HomeBase/Documents/ExampleDocument.txt";

        _logger.LogInfo($"TextEditorViewModel initialized. Current user: {userName}, default document path: {_currentlyLoadedFilePath}");

        _document = new TextDocument
        {
            Text = "This is a sample text in the editor. You can edit this text and see the changes reflected in the Content property."
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

    private async Task SaveDocumentAsync()
    {
        // Implement the logic to save the document here
        try
        {
            await _documentService.WriteAsync(_currentlyLoadedFilePath, _document.Text);
            _logger.LogInfo($"Document saved to {_currentlyLoadedFilePath}");
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
            _logger.LogInfo($"Document opened from {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to open document: {ex.Message}");
        }
    }
}