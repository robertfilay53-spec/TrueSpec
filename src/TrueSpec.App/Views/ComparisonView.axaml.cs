using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TrueSpec.App.Views;

public partial class ComparisonView : UserControl
{
    public ComparisonView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
