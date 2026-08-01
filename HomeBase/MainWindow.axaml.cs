using Avalonia.Controls;
using HomeBase.ViewModels;

namespace HomeBase;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(ChatViewModel chatViewModel)
    {
        InitializeComponent();
        DataContext = chatViewModel;
    }
}