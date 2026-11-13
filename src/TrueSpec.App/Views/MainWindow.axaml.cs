using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Security;

namespace TrueSpec.App.Views;

public partial class MainWindow : Window
{
    private readonly string _statePath;

    public MainWindow()
    {
        InitializeComponent();

        _statePath = AppDataRoot.Resolve().WindowStatePath;

        Opened += (_, _) => RestoreWindowState();
        Opened += (_, _) => WindowsTaskbar.EnsureMainWindowVisible(this);
        Opened += (_, _) => (DataContext as MainViewModel)?.BeginInitialScan();
        Closing += (_, _) => SaveWindowState();
        Closing += (_, _) => (DataContext as MainViewModel)?.Dispose();
    }

    private void RestoreWindowState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return;
            }

            var state = JsonSerializer.Deserialize<WindowStateData>(File.ReadAllText(_statePath));
            if (state is null)
            {
                return;
            }

            if (state.Width >= MinWidth && state.Height >= MinHeight)
            {
                // Clamp to the largest screen: a saved size from a bigger
                // monitor must not open a window wider than this display.
                var maxWidth = Screens.All.Max(s => (double)s.WorkingArea.Width);
                var maxHeight = Screens.All.Max(s => (double)s.WorkingArea.Height);
                Width = Math.Min(state.Width, Math.Max(MinWidth, maxWidth));
                Height = Math.Min(state.Height, Math.Max(MinHeight, maxHeight));
            }

            // Only restore the position when it lands on a visible monitor.
            // Unplugging the second monitor a window was saved on used to
            // restore it off-screen, with no way to reach it.
            var position = new PixelPoint(state.X, state.Y);
            if (Screens.All.Any(s => s.WorkingArea.Contains(position)))
            {
                Position = position;
            }
        }
        catch
        {
            // A corrupt state file must never stop the app from opening.
        }
    }

    private void SaveWindowState()
    {
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WindowStateData
            {
                X = Position.X,
                Y = Position.Y,
                Width = Width,
                Height = Height,
            };

            File.WriteAllText(_statePath, JsonSerializer.Serialize(state));
        }
        catch
        {
            // Saving window size is a convenience, never a hard failure.
        }
    }

    private sealed class WindowStateData
    {
        public int X { get; set; }

        public int Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
