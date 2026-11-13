using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using TrueSpec.App.ViewModels;

namespace TrueSpec.App.Views;

public partial class FleetView : UserControl
{
    public FleetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is FleetViewModel viewModel)
            {
                viewModel.ReportFilePicker = PickReportsAsync;
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Technician mode import (v2.1.0): picks the JSON reports to import.
    /// Mirrors the Settings view's folder picker: the StorageProvider
    /// owns the dialog, the ViewModel owns what happens with the paths.
    /// Returns an empty list when the picker is unavailable or cancelled.
    /// </summary>
    private async Task<IReadOnlyList<string>> PickReportsAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider || !provider.CanOpen)
        {
            return Array.Empty<string>();
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Choose TrueSpec JSON reports to import",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("TrueSpec JSON report")
                {
                    Patterns = new[] { "*.json" },
                },
            },
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null)
            .Select(path => path!)
            .ToArray();
    }
}
