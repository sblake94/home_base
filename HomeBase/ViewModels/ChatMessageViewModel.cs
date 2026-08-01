using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HomeBase.ViewModels
{
    public class ChatMessageViewModel : INotifyPropertyChanged
    {
        private string _text;

        public ChatMessageViewModel(string text, bool isFromUser)
        {
            _text = text;
            IsFromUser = isFromUser;
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text == value)
                {
                    return;
                }

                _text = value;
                OnPropertyChanged();
            }
        }

        public bool IsFromUser { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
