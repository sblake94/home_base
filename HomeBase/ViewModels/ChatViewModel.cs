using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;
using HomeBase.Models;
using HomeBase.Services;
namespace HomeBase.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly IChatService _chatService;
        public ObservableCollection<ChatMessageViewModel> Messages { get; } = new ObservableCollection<ChatMessageViewModel>();

        private string _inputText = string.Empty;
        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(); }
        }

        public ICommand SendMessageCommand => new RelayCommand(async _ => await SendMessage(), _ => !string.IsNullOrWhiteSpace(InputText));

        public ChatViewModel()
        {
            _chatService = new OllamaChatService("model_name", "api_key");
            Messages.Add(new ChatMessageViewModel(new ChatMessage("Welcome to the chat.", DateTime.Now, false)));
        }



        public async Task SendMessage()
        {
            var text = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) return;

            var userMsg = new ChatMessageViewModel(new ChatMessage(text, DateTime.Now, true));
            Messages.Add(userMsg);
            InputText = string.Empty;

            // Simple echo reply for demonstration
            var reply = await _chatService.SendMessage(text);
            Messages.Add(new ChatMessageViewModel(reply));
            Console.WriteLine($"Sent message: {text}");
        }

        public void OnMessageClicked(ChatMessageViewModel msg)
        {
            if (msg == null) return;
            // Simple behaviour: append a timestamp note as a new system message
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
