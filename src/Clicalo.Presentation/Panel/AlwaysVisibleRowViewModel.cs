using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-004): «📌 SIEMPRE VISIBLE», 4 columns, the tiles of the page in view and,
/// with more than it holds, the «··· i/N» chip that cycles its pages. Its tiles behave as the grid's (same view model)
/// and carry the voice numbers after the list. Capacity, pages and names come from <c>StripLayout</c>.
/// </summary>
public sealed class AlwaysVisibleRowViewModel : ObservableObject
{
    private readonly Action _nextPage;
    private bool _isVisible;
    private bool _showsLabel;
    private bool _showsNames;
    private string _label = string.Empty;
    private bool _hasMore;
    private string _moreLabel = string.Empty;
    private string _moreName = string.Empty;
    private double _tileHeightPx;

    internal AlwaysVisibleRowViewModel(Action nextPage) => _nextPage = nextPage;

    /// <summary>The tiles of the page in view, in display order.</summary>
    public ObservableCollection<TileViewModel> Tiles { get; } = [];

    /// <summary>Whether the row shows (FIJ-001).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Whether its label shows (hidden in Compact).</summary>
    public bool ShowsLabel
    {
        get => _showsLabel;
        private set => SetProperty(ref _showsLabel, value);
    }

    /// <summary>Whether the tiles show their name (not in S nor in Compact, FIJ-002).</summary>
    public bool ShowsNames
    {
        get => _showsNames;
        private set => SetProperty(ref _showsNames, value);
    }

    /// <summary>[always], localized; the view shows it in capitals.</summary>
    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    /// <summary>Whether the «··· i/N» chip shows (FIJ-003).</summary>
    public bool HasMore
    {
        get => _hasMore;
        private set => SetProperty(ref _hasMore, value);
    }

    /// <summary>The «i/N» of the chip.</summary>
    public string MoreLabel
    {
        get => _moreLabel;
        private set => SetProperty(ref _moreLabel, value);
    }

    /// <summary>[stripMoreA], the accessible name of the chip.</summary>
    public string MoreName
    {
        get => _moreName;
        private set => SetProperty(ref _moreName, value);
    }

    /// <summary>Visual height of a tile of the row (FIJ-002).</summary>
    public double TileHeightPx
    {
        get => _tileHeightPx;
        private set => SetProperty(ref _tileHeightPx, value);
    }

    /// <summary>The chip (or UI Automation Invoke): the next page, and the first after the last.</summary>
    public void More() => _nextPage();

    internal void Apply(
        IReadOnlyList<TileViewModel> tiles,
        bool visible,
        bool showsLabel,
        bool showsNames,
        string label,
        bool hasMore,
        string moreLabel,
        string moreName,
        double tileHeightPx
    )
    {
        PanelViewModel.Sync(Tiles, tiles);
        Label = label;
        ShowsLabel = showsLabel;
        ShowsNames = showsNames;
        HasMore = hasMore;
        MoreLabel = moreLabel;
        MoreName = moreName;
        TileHeightPx = tileHeightPx;
        IsVisible = visible;
    }
}
