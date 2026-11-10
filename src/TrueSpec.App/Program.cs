using Avalonia;
using System;
using TrueSpec.App.Services;

namespace TrueSpec.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // One instance only: a second launch brings the running window to the
        // front instead of starting a duplicate process.
        using var singleInstance = SingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            SingleInstanceGuard.ActivateExistingInstance();
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
