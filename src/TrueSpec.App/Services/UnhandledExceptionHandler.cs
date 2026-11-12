using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TrueSpec.App.Services;

/// <summary>
/// Last-resort handler for exceptions that escape every other catch: the
/// app used to exit silently on one, leaving the user with no window and no
/// explanation. Now the exception is appended to the startup log and the
/// user gets a plain-language dialog instead of a silent exit. Nothing here
/// ever throws: a failing handler must not become a second crash.
/// </summary>
public static class UnhandledExceptionHandler
{
    // One dialog per process: a faulting loop must not stack windows.
    private static int _dialogShown;

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Report(args.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Report(args.Exception, "Unobserved task exception");
            // The fault is already logged; do not let the runtime escalate
            // it into a second crash at finalization time.
            args.SetObserved();
        };
    }

    /// <summary>
    /// UI-thread exceptions. Called from <see cref="App.OnFrameworkInitializationCompleted"/>
    /// because the dispatcher only exists once Avalonia is initialized.
    /// Marking the event handled keeps the app alive after a UI fault
    /// instead of tearing the window down.
    /// </summary>
    public static void InstallDispatcher()
    {
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            Report(args.Exception, "UI thread exception");
            args.Handled = true;
        };
    }

    private static void Report(Exception? exception, string source)
    {
        var detail = exception is null
            ? $"{source} (no exception object was provided)."
            : $"{source}: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception.StackTrace}";

        try
        {
            StartupLog.Append(detail);
        }
        catch
        {
            // Logging must never break the handler.
        }

        if (Interlocked.Exchange(ref _dialogShown, 1) != 0)
        {
            return;
        }

        try
        {
            Dispatcher.UIThread.Post(() => ShowDialog(detail));
        }
        catch
        {
            // No dispatcher (very early startup fault): the log entry is
            // the evidence, and there is no window to show it in.
        }
    }

    private static void ShowDialog(string detail)
    {
        try
        {
            var window = new Window
            {
                Title = "TrueSpec ran into a problem",
                Width = 640,
                Height = 360,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = "TrueSpec ran into an unexpected problem and could not keep going. "
                            + "Nothing was uploaded anywhere: TrueSpec works offline, and the details of "
                            + "what happened were written to the startup log so the problem can be reported. "
                            + "Please close this window and start TrueSpec again."
                            + Environment.NewLine + Environment.NewLine + detail,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                },
            };
            window.Show();
        }
        catch
        {
            // Showing the dialog must never throw into the handler.
        }
    }
}
