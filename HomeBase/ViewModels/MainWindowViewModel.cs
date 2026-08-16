using System.Windows.Input;
using HomeBase.Commands;

namespace HomeBase.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ChatViewModel _chatViewModel;
    private readonly TextEditorViewModel _textEditorViewModel;

    public MainWindowViewModel(
        ChatViewModel chatViewModel,
        TextEditorViewModel textEditorViewModel)
    {
        _chatViewModel = chatViewModel;
        _textEditorViewModel = textEditorViewModel;
    }

    public ChatViewModel ChatViewModel => _chatViewModel;
    public TextEditorViewModel TextEditorViewModel => _textEditorViewModel;

    public ICommand OpenDocumentCommand => new RelayCommand(_ => OpenDocument(), _ => true);
    public ICommand SaveDocumentCommand => new RelayCommand(_ => SaveDocument(), _ => true);

    private void OpenDocument()
    {
        // Implement the logic to open a document here

    }

    private void SaveDocument()
    {
        // Implement the logic to save a document here
    }
}