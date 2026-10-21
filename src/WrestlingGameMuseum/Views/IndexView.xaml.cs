using System.Windows;
using System.Windows.Controls;
using WrestlingGameMuseum.ViewModels;

namespace WrestlingGameMuseum.Views;

public partial class IndexView : UserControl
{
    private const double SideGutters = 16 + 6 + 10 + 18; // outer margins, tile gap and the scroll bar

    public IndexView() => InitializeComponent();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SetViewportWidth(e.NewSize.Width - SideGutters);
    }
}
