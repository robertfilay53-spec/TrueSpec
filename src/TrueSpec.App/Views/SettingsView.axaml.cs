using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TrueSpec.App.ViewModels;

namespace TrueSpec.App.Views;

/// <summary>
/// Settings view. The only platform-specific piece here is the folder
/// picker: the view model asks for a folder through a delegate, and this
/// code-behind answers it with Avalonia's native storage provider. The
/// chosen path is still validated by PathSafety before it is stored.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.FolderPicker = PickFolderAsync;
            }
        };
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider || !provider.CanPickFolder)
        {
            return null;
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose the TrueSpec export folder",
        });

        var folder = folders.FirstOrDefault();
        return folder?.TryGetLocalPath();
    }
}
