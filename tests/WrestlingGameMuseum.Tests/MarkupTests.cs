using System.Xml.Linq;

namespace WrestlingGameMuseum.Tests;

/// <summary>
/// The cloud build cannot render WPF, so these read the XAML as XML and enforce the owner's rules:
/// icon-only buttons carry a ToolTip and an AutomationProperties.Name, inputs have names, one slim toolbar per view.
/// </summary>
public class MarkupTests
{
    private static readonly XNamespace Ui = "http://schemas.lepo.co/wpfui/2022/xaml";
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] Files = ["Views/IndexView.xaml", "Views/ExhibitView.xaml", "MainWindow.xaml"];

    private static XDocument Load(string file) =>
        XDocument.Load(RepoPaths.Path("src", "WrestlingGameMuseum", file));

    private static bool Has(XElement e, string attr) => e.Attributes().Any(a => a.Name.LocalName == attr && a.Value.Length > 0);

    private static bool HasAutomationName(XElement e) => Has(e, "AutomationProperties.Name");

    public static IEnumerable<object[]> ViewFiles => Files.Select(f => new object[] { f });

    [Theory, MemberData(nameof(ViewFiles))]
    public void IconOnlyButtonsHaveToolTipAndAutomationName(string file)
    {
        var iconOnly = Load(file).Descendants(Ui + "Button")
            .Where(b => Has(b, "Icon") && !Has(b, "Content")).ToList();
        foreach (XElement button in iconOnly)
        {
            string id = button.Attribute("Icon")?.Value ?? "?";
            Assert.True(Has(button, "ToolTip"), $"{file}: icon button {id} needs a ToolTip");
            Assert.True(HasAutomationName(button), $"{file}: icon button {id} needs AutomationProperties.Name");
        }
        if (file != "MainWindow.xaml") Assert.NotEmpty(iconOnly);
    }

    [Theory, MemberData(nameof(ViewFiles))]
    public void EveryButtonAndInputIsNamedForScreenReaders(string file)
    {
        var doc = Load(file);
        var controls = doc.Descendants().Where(e =>
            (e.Name == Wpf + "Button") || (e.Name == Ui + "Button") || e.Name == Ui + "TextBox" || e.Name == Wpf + "ComboBox");
        foreach (XElement c in controls)
            Assert.True(HasAutomationName(c) || Has(c, "Content"), $"{file}: <{c.Name.LocalName}> has no accessible name");
    }

    [Fact]
    public void IndexHasOneSlimToolbarWithSearchAndFilterDrawer()
    {
        var doc = Load("Views/IndexView.xaml");
        Assert.Single(doc.Descendants(Wpf + "Border"), b => b.Attribute("Style")?.Value.Contains("ToolbarBand") == true);
        Assert.Contains(doc.Descendants(Ui + "TextBox"), t => Has(t, "PlaceholderText"));
        foreach (string filter in new[] { "era", "platform", "promotion or licence", "developer", "series" })
            Assert.Contains(doc.Descendants(Wpf + "ComboBox"), c => c.Attribute("AutomationProperties.Name")?.Value.Contains(filter) == true);
        Assert.Contains("Summary", doc.ToString());
    }

    [Fact]
    public void TileFollowsTheSpec()
    {
        string xaml = File.ReadAllText(RepoPaths.Path("src", "WrestlingGameMuseum", "Views", "IndexView.xaml"));
        Assert.Contains("BlurRadius=\"20\"", xaml);
        Assert.Contains("ShadowDepth=\"7\"", xaml);
        Assert.Contains("Direction=\"270\"", xaml);
        Assert.Contains("Opacity=\"0.7\"", xaml);
        Assert.Contains("Stretch=\"Uniform\"", xaml);
        Assert.Contains("VerticalAlignment=\"Bottom\"", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml);
        Assert.DoesNotContain("EXPLORE", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThemeCarriesTheFireProMuseumTokens()
    {
        string theme = File.ReadAllText(RepoPaths.Path("src", "WrestlingGameMuseum", "Theme", "Museum.xaml"));
        foreach (string token in new[]
                 {
                     "#D91E1C1A", "#ED100D0D", "#A568421D", "#FFFFBE65", "#FF261A14", "#FFFFD787",
                     "#FF0C0B0B", "#FF1A1512", "#FF3A2A1C", "#FFFFC977", "#FFFFCD76", "#33FFC977", "#1FFFFFFF",
                 })
            Assert.Contains(token, theme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExhibitHasBackPreviousNextAndAllCards()
    {
        string xaml = File.ReadAllText(RepoPaths.Path("src", "WrestlingGameMuseum", "Views", "ExhibitView.xaml"));
        foreach (string needle in new[]
                 {
                     "CloseExhibitCommand", "PreviousGameCommand", "NextGameCommand", "Regional releases", "Screenshots", "Videos",
                     "Roster research in progress", "Sources &amp; credits", "Find screenshots", "Find gameplay videos", "THE STORY", "PLAY AND FEATURES",
                 })
            Assert.Contains(needle, xaml);
    }

    [Fact]
    public void MinimumWindowWidthIs900()
    {
        Assert.Equal("900", Load("MainWindow.xaml").Root!.Attribute("MinWidth")?.Value);
    }
}
