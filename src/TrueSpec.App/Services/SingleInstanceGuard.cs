using System;
using System.Runtime.InteropServices;
using System.Threading;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Services;

/// <summary>
/// One instance only. The first launch owns a named mutex for the lifetime
/// of the process; a second launch finds the mutex taken, asks the existing
/// window to come to the front, and exits without starting a duplicate
/// process. The mutex name is session-scoped ("Local\") so two different
/// RDP users on the same machine are not blocked by each other, and
/// per-user (see <see cref="DefaultMutexName"/>) so another local user
/// cannot squat the name and block launch.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>Base name; the live mutex appends a per-user suffix (see <see cref="DefaultMutexName"/>).</summary>
    public const string MutexName = "Local\\TrueSpec.SingleInstance";

    /// <summary>
    /// The mutex name the app actually uses: the base name plus a hash of
    /// the current user. A fixed well-known name lets any other local
    /// process create the mutex first and block TrueSpec from launching;
    /// the per-user suffix stops another user in the same session from
    /// squatting it. Only the hash is in the name, never the username.
    /// </summary>
    public static string DefaultMutexName() =>
        MutexName + "." + SingleInstanceIdentity.PerUserSuffix();

    /// <summary>Must match Title in Views/MainWindow.axaml.</summary>
    public const string MainWindowTitle = "TrueSpec - Command Center";

    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    /// <summary>
    /// Tries to become the single instance. Returns the guard on success, or
    /// null when another instance already owns the mutex. Never throws.
    /// A null name uses the per-user default (see <see cref="DefaultMutexName"/>);
    /// an explicit name is only for tests.
    /// </summary>
    public static SingleInstanceGuard? TryAcquire(string? name = null)
    {
        name ??= DefaultMutexName();
        Mutex mutex;
        bool createdNew;
        try
        {
            mutex = new Mutex(initiallyOwned: false, name, out createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            // Another user owns the mutex: treat this as a second instance.
            return null;
        }
        catch
        {
            return null;
        }

        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing: we own it now.
            acquired = true;
        }
        catch
        {
            mutex.Dispose();
            return null;
        }

        if (!acquired)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstanceGuard(mutex);
    }

    /// <summary>
    /// Best-effort: restore and foreground the first instance's window.
    /// Windows only; on any other OS this is a no-op that returns false.
    /// Never throws.
    /// </summary>
    public static bool ActivateExistingInstance(string windowTitle = MainWindowTitle)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var handle = FindWindow(null, windowTitle);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            ShowWindow(handle, SW_RESTORE);
            return SetForegroundWindow(handle);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Already released, or the mutex was abandoned: nothing to do.
        }

        _mutex.Dispose();
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
