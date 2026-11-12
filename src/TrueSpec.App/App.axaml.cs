using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.App.Views;

namespace TrueSpec.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupLog.Append("App: starting");
        UnhandledExceptionHandler.InstallDispatcher();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
            StartupLog.Append("App: MainWindow created");
        }

        base.OnFrameworkInitializationCompleted();
    }
}