using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HomeBase.ViewModels;
using Avalonia.Markup.Xaml;

namespace HomeBase.Views
{
    public partial class ChatView : UserControl
    {
        private ChatViewModel? VM => DataContext as ChatViewModel;

        public ChatView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
