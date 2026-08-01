using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HomeBase.ViewModels;

namespace HomeBase.Selectors;

public class ChatMessageTemplateSelector : IDataTemplate
{
    // Set from XAML to the DataTemplate resources to choose between.
    public IDataTemplate? UserMessageTemplate { get; set; }
    public IDataTemplate? OtherMessageTemplate { get; set; }

    public Control? Build(object? param)
    {
        if (param is not ChatMessageViewModel message)
            return null;

        var template = message.IsFromUser ? UserMessageTemplate : OtherMessageTemplate;
        return template?.Build(param);
    }

    public bool Match(object? data) => data is ChatMessageViewModel;
}