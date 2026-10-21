using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WrestlingGameMuseum.Core;
using WrestlingGameMuseum.Services;

namespace WrestlingGameMuseum.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IReadOnlyList<Game> _all;
    private double _viewportWidth = 1200;
    private int _columns = 3;
    private double _imageHeight = 324;
    private bool _suspend;

    public MainViewModel(IReadOnlyList<Game> games)
    {
        _all = games;
        Eras = GameQuery.Options(games, g => [g.Era]);
        Platforms = GameQuery.Options(games, g => g.Platforms);
        Promotions = GameQuery.Options(games, g => g.Promotions);
        Developers = GameQuery.Options(games, g => [g.Developer]);
        SeriesOptions = GameQuery.Options(games, g => [g.Series]);
        Refresh();
    }

    public IReadOnlyList<string> Eras { get; }
    public IReadOnlyList<string> Platforms { get; }
    public IReadOnlyList<string> Promotions { get; }
    public IReadOnlyList<string> Developers { get; }
    public IReadOnlyList<string> SeriesOptions { get; }

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedEra = GameQuery.Any;
    [ObservableProperty] private string _selectedPlatform = GameQuery.Any;
    [ObservableProperty] private string _selectedPromotion = GameQuery.Any;
    [ObservableProperty] private string _selectedDeveloper = GameQuery.Any;
    [ObservableProperty] private string _selectedSeries = GameQuery.Any;
    [ObservableProperty] private SortOrder _sort = SortOrder.ReleaseDate;
    [ObservableProperty] private Grouping _grouping = Grouping.Era;
    [ObservableProperty] private bool _filtersOpen;

    [ObservableProperty] private IReadOnlyList<IndexRow> _rows = [];
    [ObservableProperty] private IReadOnlyList<Game> _filtered = [];
    [ObservableProperty] private string _summary = "";

    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private string _rosterSearch = "";
    [ObservableProperty] private IReadOnlyList<RosterEntry> _rosterRows = [];
    [ObservableProperty] private string _status = "";

    public bool IsBrowsing => SelectedGame is null;
    public bool IsViewingExhibit => SelectedGame is not null;
    public bool FiltersActive =>
        SearchText.Trim().Length > 0 || SelectedEra != GameQuery.Any || SelectedPlatform != GameQuery.Any ||
        SelectedPromotion != GameQuery.Any || SelectedDeveloper != GameQuery.Any || SelectedSeries != GameQuery.Any;

    public string SortTip => Sort == SortOrder.ReleaseDate
        ? "Sorted by release date. Click to sort by title."
        : "Sorted by title. Click to sort by release date.";

    public string GroupTip => Grouping switch
    {
        Grouping.Era => "Shelved by era. Click to shelve by series.",
        Grouping.Series => "Shelved by series. Click to shelve by developer.",
        _ => "Shelved by developer. Click to shelve by era.",
    };

    public string ExhibitLabel => SelectedGame is { Year: > 0 } g ? "EXHIBIT · " + g.Year : "EXHIBIT";
    public string ExhibitPosition => SelectedGame is { } g && Filtered.ToList().FindIndex(x => x.Id == g.Id) is >= 0 and var i
        ? $"{i + 1} of {Filtered.Count}" : "";

    public bool HasRoster => SelectedGame is { Roster.Count: > 0 };
    public bool RosterInProgress => SelectedGame is { Roster.Count: 0 };

    public IReadOnlyList<RegionalRelease> RegionalReleases => SelectedGame?.Regional ?? [];

    /// <summary>The index tells the view how wide it is so the shelves can pick 3–5 columns.</summary>
    public void SetViewportWidth(double width)
    {
        if (width < 100) return;
        _viewportWidth = width;
        int columns = IndexLayout.ColumnsFor(width);
        double imageHeight = IndexLayout.ImageHeightFor(width, columns);
        if (columns == _columns && Math.Abs(imageHeight - _imageHeight) < 0.5) return;
        Refresh();
    }

    private void Refresh()
    {
        if (_suspend) return;
        var query = new GameQuery
        {
            Search = SearchText, Era = SelectedEra, Platform = SelectedPlatform, Promotion = SelectedPromotion,
            Developer = SelectedDeveloper, Series = SelectedSeries, Sort = Sort, Group = Grouping,
        };
        Filtered = query.Apply(_all);
        _columns = IndexLayout.ColumnsFor(_viewportWidth);
        _imageHeight = IndexLayout.ImageHeightFor(_viewportWidth, _columns);
        Rows = IndexLayout.Build(query.Shelves(Filtered), _columns, _imageHeight, FiltersActive);
        Summary = BuildSummary();
        OnPropertyChanged(nameof(FiltersActive));
        OnPropertyChanged(nameof(ExhibitPosition));
    }

    private string BuildSummary()
    {
        var dated = _all.Where(g => g.Year > 0).Select(g => g.Year).ToList();
        string span = dated.Count == 0 ? "" : dated.Min() == dated.Max() ? " · " + dated.Min() : $" · {dated.Min()}–{dated.Max()}";
        return Filtered.Count == _all.Count
            ? $"{_all.Count:N0} games{span}"
            : $"{Filtered.Count:N0} of {_all.Count:N0} games{span}";
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnSelectedEraChanged(string value) => Refresh();
    partial void OnSelectedPlatformChanged(string value) => Refresh();
    partial void OnSelectedPromotionChanged(string value) => Refresh();
    partial void OnSelectedDeveloperChanged(string value) => Refresh();
    partial void OnSelectedSeriesChanged(string value) => Refresh();

    partial void OnSortChanged(SortOrder value)
    {
        OnPropertyChanged(nameof(SortTip));
        Refresh();
    }

    partial void OnGroupingChanged(Grouping value)
    {
        OnPropertyChanged(nameof(GroupTip));
        Refresh();
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        OnPropertyChanged(nameof(IsBrowsing));
        OnPropertyChanged(nameof(IsViewingExhibit));
        OnPropertyChanged(nameof(ExhibitLabel));
        OnPropertyChanged(nameof(ExhibitPosition));
        OnPropertyChanged(nameof(HasRoster));
        OnPropertyChanged(nameof(RosterInProgress));
        OnPropertyChanged(nameof(RegionalReleases));
        RosterSearch = "";
        UpdateRoster();
        Status = "";
    }

    partial void OnRosterSearchChanged(string value) => UpdateRoster();

    private void UpdateRoster()
    {
        string term = RosterSearch.Trim();
        RosterRows = (SelectedGame?.Roster ?? []).Where(r => term.Length == 0 ||
            r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            r.RealName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            r.Group.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    [RelayCommand] private void OpenGame(Game? game) { if (game is not null) SelectedGame = game; }
    [RelayCommand] private void CloseExhibit() => SelectedGame = null;
    [RelayCommand] private void ToggleFilters() => FiltersOpen = !FiltersOpen;
    [RelayCommand] private void ToggleSort() => Sort = Sort == SortOrder.ReleaseDate ? SortOrder.Title : SortOrder.ReleaseDate;

    [RelayCommand]
    private void CycleGrouping() => Grouping = Grouping switch
    {
        Grouping.Era => Grouping.Series,
        Grouping.Series => Grouping.Developer,
        _ => Grouping.Era,
    };

    [RelayCommand]
    private void ClearFilters()
    {
        _suspend = true;
        SearchText = "";
        SelectedEra = SelectedPlatform = SelectedPromotion = SelectedDeveloper = SelectedSeries = GameQuery.Any;
        _suspend = false;
        Refresh();
    }

    /// <summary>Previous/next walk the current filter, wrapping at the ends.</summary>
    [RelayCommand] private void PreviousGame() => Step(-1);
    [RelayCommand] private void NextGame() => Step(1);

    private void Step(int offset)
    {
        if (SelectedGame is null || Filtered.Count == 0) return;
        int index = Filtered.ToList().FindIndex(g => g.Id == SelectedGame.Id);
        SelectedGame = index < 0 ? Filtered[0] : Filtered[(index + offset + Filtered.Count) % Filtered.Count];
    }

    [RelayCommand]
    private void OpenLink(string? url) =>
        Status = UrlLauncher.TryOpen(url) ? "" : "That link could not be opened.";

    [RelayCommand]
    private void OpenMedia(MediaLink? media) => OpenLink(media?.SourceUrl);

    [RelayCommand]
    private void FindScreenshots(Game? game)
    {
        if (game is not null)
            OpenLink("https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(game.Title + " " + game.PlatformText + " gameplay screenshots"));
    }

    [RelayCommand]
    private void FindVideos(Game? game)
    {
        if (game is not null)
            OpenLink("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(game.Title + " " + game.PlatformText + " gameplay"));
    }
}
