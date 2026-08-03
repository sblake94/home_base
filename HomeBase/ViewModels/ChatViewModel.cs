using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using HomeBase.Services;
using HomeBase.Services.ChatService;
using System;
using HomeBase.Utils;
using Avalonia.Threading;

namespace HomeBase.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly IChatService _chatService;
        private readonly IBackendStatusService _backendStatusService;
        private readonly Logger<ChatViewModel> _log;
        private readonly RelayCommand _sendMessageCommand;
        private readonly RelayCommand _cancelSendCommand;
        private CancellationTokenSource? _sendCancellation;

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
                _cancelSendCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isBackendAvailable = true;
        public bool IsBackendAvailable
        {
            get => _isBackendAvailable;
            private set
            {
                _isBackendAvailable = value;
                OnPropertyChanged();
                _sendMessageCommand.RaiseCanExecuteChanged();
            }
        }

        private string _backendStatusMessage = string.Empty;
        public string BackendStatusMessage
        {
            get => _backendStatusMessage;
            private set
            {
                _backendStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand SendMessageCommand => _sendMessageCommand;
        public ICommand CancelSendCommand => _cancelSendCommand;

        public ChatViewModel(IChatService chatService, IBackendStatusService backendStatusService, Logger<ChatViewModel> log)
        {
            _chatService = chatService;
            _backendStatusService = backendStatusService;
            _log = log;
            _sendMessageCommand = new RelayCommand(async _ => await SendMessage(), _ => IsBackendAvailable && !IsSending && !string.IsNullOrWhiteSpace(InputText));
            _cancelSendCommand = new RelayCommand(_ =>
            {
                try
                {
                    _sendCancellation?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }, _ => IsSending);
            
            _ = RefreshBackendStatusAsync();
        }

        public async Task RefreshBackendStatusAsync()
        {
            var (isReady, message) = await _backendStatusService.GetStatusAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBackendAvailable = isReady;
                BackendStatusMessage = message;
            });
        }

        public async Task SendMessage()
        {
            var text = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text) || IsSending || !IsBackendAvailable)
            {
                return;
            }

            IsSending = true;
            InputText = string.Empty;

            var assistantMessage = new ChatMessageViewModel(string.Empty, false);
            Messages.Add(new ChatMessageViewModel(text, true));
            Messages.Add(assistantMessage);

            _sendCancellation = new CancellationTokenSource();

            try
            {
                await foreach (var token in _chatService.SubmitUserMessageAsync(text, _sendCancellation.Token).ConfigureAwait(false))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += token);
                }

                _log.LogInformation($"User message submitted: {text}");
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += "\n[cancelled]");
            }
            catch (CoreChatException exception)
            {
                _log.LogInformation($"Chat backend reported an error: {exception.Code} - {exception.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += $"\n{exception.Message}");
            }
            catch (Exception exception)
            {
                _log.LogInformation($"Failed to submit user message: {exception.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => assistantMessage.Text += "\nUnable to receive a response.");
                await RefreshBackendStatusAsync();
            }
            finally
            {
                _sendCancellation.Dispose();
                _sendCancellation = null;
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
