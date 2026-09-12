using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HomeBase.Views;

public partial class TextEditorView : UserControl
{
    public TextEditorView()
    {
        InitializeComponent();
        DocumentContents.Options = new AvaloniaEdit.TextEditorOptions
        {
            IndentationSize = 4
        };
    }
}