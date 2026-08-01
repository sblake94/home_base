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

        private void ChatView_DataContextChanged(object? sender, System.EventArgs e)
        {
            if (VM is null)
            {
                return;
            }

            VM.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || _messagesList is null || VM is null || VM.Messages.Count == 0)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => _messagesList.ScrollIntoView(VM.Messages[^1]));
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

