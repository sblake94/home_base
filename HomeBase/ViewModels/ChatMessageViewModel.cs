using System;
using HomeBase.Models;

namespace HomeBase.ViewModels
{
    public class ChatMessageViewModel(ChatMessage model)
    {
        public string Text => model.Text;
        public DateTime Timestamp => model.Timestamp;
        public bool IsFromUser => model.IsFromUser;
    }
}
