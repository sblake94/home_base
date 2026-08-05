using System;
using System.Windows.Input;
using AvaloniaEdit.Document;
using HomeBase.Commands;

namespace HomeBase.ViewModels;

public sealed class TextEditorViewModel : ViewModelBase
{
    private string _filePath = string.Empty;

    public TextEditorViewModel()
    {
        _document = new TextDocument
        {
            Text = "This is a sample text in the editor. You can edit this text and see the changes reflected in the Content property."
        };

        _document.TextChanged += (sender, e) =>
        {
            OnPropertyChanged(nameof(Content));
        };

        _filePath = "/home/sam/HomeBase/Docs/ExampleDocument.txt";

        Console.WriteLine("TextEditorViewModel initialized with sample text.");
    }
    

    public string Content => _document.Text;

    public ICommand SaveCommand => new RelayCommand(_ => SaveDocument(), _ => true);
    public ICommand OpenCommand => new RelayCommand(_ => OpenDocument(_filePath), _ => true);

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

    private void SaveDocument()
    {
        // Implement the logic to save the document here
        Console.WriteLine("SaveDocument method called. Implement saving logic here.");
    }

    private void OpenDocument(string path)
    {
        // Implement the logic to open a document here
        Console.WriteLine($"OpenDocument method called with path: {path}. Implement opening logic here.");
    }
}