using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace TrueSpec.App.Services;

/// <summary>
/// Windows shell behaviour for the main window. Avalonia normally gives a
/// top-level window a taskbar button, but a real-Windows test showed the
/// TrueSpec window missing from the taskbar and Alt-Tab, so the native
/// extended style is enforced explicitly: WS_EX_APPWINDOW on,
/// WS_EX_TOOLWINDOW off, then a frame refresh so Explorer picks the change
/// up. Windows only; every call is guarded and best-effort, so a failure
/// here can never break startup.
/// </summary>
public static class WindowsTaskbar
{
    public static void EnsureMainWindowVisible(Window window)
    {
        if (window is null || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (handle is null || handle == IntPtr.Zero)
            {
                return;
            }

            var hwnd = handle.Value;
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            var updated = (style | WS_EX_APPWINDOW) & ~WS_EX_TOOLWINDOW;
            if (updated != style)
            {
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(updated));
            }

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
        catch
        {
            // Taskbar presence is cosmetic: never break the app over it.
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
