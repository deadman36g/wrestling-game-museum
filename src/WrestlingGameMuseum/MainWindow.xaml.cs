using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Ui.Controls;
using WrestlingGameMuseum.Core;
using WrestlingGameMuseum.ViewModels;

namespace WrestlingGameMuseum;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(LoadGames(out string problem));
        if (problem.Length > 0) _vm.Status = problem;
        DataContext = _vm;
    }

    private static IReadOnlyList<Game> LoadGames(out string problem)
    {
        problem = "";
        string path = Path.Combine(AppContext.BaseDirectory, "data", "games.json");
        try { return CatalogLoader.Load(path).Games; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            problem = "The collection file could not be read: " + ex.Message;
            return [];
        }
    }

    /// <summary>Backspace / Esc go back to the collection; the arrows page through games. Typing in a box is never hijacked.</summary>
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_vm.IsViewingExhibit || Keyboard.FocusedElement is TextBoxBase or System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
        switch (e.Key)
        {
            case Key.Back or Key.Escape: _vm.CloseExhibitCommand.Execute(null); break;
            case Key.Left: _vm.PreviousGameCommand.Execute(null); break;
            case Key.Right: _vm.NextGameCommand.Execute(null); break;
            default: return;
        }
        e.Handled = true;
    }
}
