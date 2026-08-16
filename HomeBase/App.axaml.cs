using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HomeBase.DependencyInjection;

namespace HomeBase;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = ServiceManager.GetRequiredService<ViewModels.MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(mainWindowViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}