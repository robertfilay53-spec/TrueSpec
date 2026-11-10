using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;

namespace TrueSpec.App.Views;

public partial class MainWindow : Window
{
    private readonly string _statePath;

    public MainWindow()
    {
        InitializeComponent();

        _statePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueSpec",
            "window-state.json");

        Opened += (_, _) => RestoreWindowState();
        Opened += (_, _) => WindowsTaskbar.EnsureMainWindowVisible(this);
        Opened += (_, _) => (DataContext as MainViewModel)?.BeginInitialScan();
        Closing += (_, _) => SaveWindowState();
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
                Width = state.Width;
                Height = state.Height;
            }

            Position = new PixelPoint(state.X, state.Y);
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
