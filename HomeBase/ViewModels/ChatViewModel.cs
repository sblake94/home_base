using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;
using HomeBase.Services.ChatService;
using System;
using HomeBase.Utils;
using Avalonia.Threading;

namespace HomeBase.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly OllamaChatService _chatService;
        private readonly Logger<ChatViewModel> _log;
        private readonly RelayCommand _sendMessageCommand;

        public ObservableCollection<ChatMessageViewModel> Messages { get; } = new ObservableCollection<ChatMessageViewModel>();

        private string _inputText = string.Empty;
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
                _sendMessageCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            private set
            {
                _isSending = value;
                OnPropertyChanged();
                _sendMessageCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand SendMessageCommand => _sendMessageCommand;

        public ChatViewModel(OllamaChatService chatService, Logger<ChatViewModel> log)
        {
            _chatService = chatService;
            _log = log;
            _sendMessageCommand = new RelayCommand(async _ => await SendMessage(), _ => !IsSending && !string.IsNullOrWhiteSpace(InputText));
        }

        public async Task SendMessage()
        {
            var text = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text) || IsSending)
            {
                return;
            }

            IsSending = true;
            InputText = string.Empty;

            var assistantMessage = new ChatMessageViewModel(string.Empty, false);
            Messages.Add(new ChatMessageViewModel(text, true));
            Messages.Add(assistantMessage);

            try
            {
                await foreach (var token in _chatService.SubmitUserMessageAsync(text).ConfigureAwait(false))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += token);
                }

                _log.LogInformation($"User message submitted: {text}");
            }
            catch (Exception exception)
            {
                _log.LogInformation($"Failed to submit user message: {exception.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += "\nUnable to receive a response.");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsSending = false);
            }
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
