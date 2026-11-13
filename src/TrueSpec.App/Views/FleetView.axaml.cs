using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TrueSpec.App.Views;

public partial class FleetView : UserControl
{
    public FleetView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
