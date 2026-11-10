using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace TrueSpec.App.Views;

public partial class ComponentDetailView : UserControl
{
    public ComponentDetailView()
    {
        InitializeComponent();
    }

    private async void CopyValue_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(value);
        }
    }
}
