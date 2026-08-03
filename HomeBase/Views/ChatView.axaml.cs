using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HomeBase.ViewModels;
using Avalonia.Markup.Xaml;
using System.Collections.Specialized;

namespace HomeBase.Views
{
    public partial class ChatView : UserControl
    {
        private ChatViewModel? VM => DataContext as ChatViewModel;
        private ListBox? _messagesList;

        public ChatView()
        {
            InitializeComponent();
            DataContextChanged += ChatView_DataContextChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _messagesList = this.FindControl<ListBox>("MessagesList");
        }
        private System.Collections.Specialized.INotifyCollectionChanged? _subscribedMessages;

        private void ChatView_DataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedMessages is not null)
            {
                _subscribedMessages.CollectionChanged -= Messages_CollectionChanged;
                _subscribedMessages = null;
            }

            if (VM?.Messages is null)
            {
                return;
            }

            _subscribedMessages = VM.Messages;
            _subscribedMessages.CollectionChanged += Messages_CollectionChanged;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || _messagesList is null)
            {
                return;
            }

            if (sender is not System.Collections.IList list || list.Count == 0)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => _messagesList.ScrollIntoView(list[list.Count - 1]));
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = VM?.SendMessage();
                e.Handled = true;
            }
        }

        private void Message_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control c && c.DataContext is ChatMessageViewModel msg)
            {
                VM?.OnMessageClicked(msg);
            }
        }
    }
}

