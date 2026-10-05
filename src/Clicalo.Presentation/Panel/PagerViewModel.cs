using System.Collections.ObjectModel;
using Clicalo.Domain.PanelLayout;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// ◀, the page dots and ▶ under the grid (CUA-004), and the swipe that is their gesture (CUA-005). The page itself and
/// every bound come from <see cref="Paging"/>; this only shows them and forwards the page asked for.
/// </summary>
public sealed class PagerViewModel : ObservableObject
{
    private readonly Action<int> _goTo;
    private PageWindow _window = Paging.Window(0, 1, 0);
    private bool _isVisible;
    private string _previousName = string.Empty;
    private string _nextName = string.Empty;

    internal PagerViewModel(Action<int> goTo) => _goTo = goTo;

    /// <summary>The dots, one per page; none with a single page.</summary>
    public ObservableCollection<PageDotViewModel> Dots { get; } = [];

    /// <summary>Whether ◀, the dots and ▶ show (more than one page, Full view).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The page in view, from 0.</summary>
    public int Page => _window.Page;

    /// <summary>Pages.</summary>
    public int PageCount => _window.PageCount;

    /// <summary>Whether ◀ leads somewhere; it dims on the first page.</summary>
    public bool CanGoPrevious => _window.HasPrevious;

    /// <summary>Whether ▶ leads somewhere; it dims on the last page.</summary>
    public bool CanGoNext => _window.HasNext;

    /// <summary>[prevPage].</summary>
    public string PreviousName
    {
        get => _previousName;
        private set => SetProperty(ref _previousName, value);
    }

    /// <summary>[nextPage].</summary>
    public string NextName
    {
        get => _nextName;
        private set => SetProperty(ref _nextName, value);
    }

    /// <summary>◀ (or UI Automation Invoke).</summary>
    public void Previous() => _goTo(Paging.Step(_window, -1));

    /// <summary>▶ (or UI Automation Invoke).</summary>
    public void Next() => _goTo(Paging.Step(_window, 1));

    /// <summary>A horizontal swipe on the grid (CUA-005): toward the left, the next page; toward the right, the previous one.</summary>
    /// <param name="towardLeft">Whether the finger moved toward the left.</param>
    public void Swiped(bool towardLeft) => _goTo(Paging.AfterSwipe(_window, towardLeft));

    internal void Apply(
        PageWindow window,
        bool visible,
        string previousName,
        string nextName,
        string pageWord,
        string currentWord
    )
    {
        var moved = window != _window;
        _window = window;
        PreviousName = previousName;
        NextName = nextName;
        var dots = Paging.Dots(window);
        if (Dots.Count != dots.Length)
        {
            Dots.Clear();
            foreach (var dot in dots)
            {
                Dots.Add(new PageDotViewModel(dot.Page, _goTo));
            }
        }

        for (var i = 0; i < dots.Length; i++)
        {
            Dots[i]
                .Apply(
                    dots[i].IsActive,
                    dots[i].WidthPx,
                    Paging.DotName(pageWord, dots[i].Page),
                    dots[i].IsActive ? currentWord : string.Empty
                );
        }

        IsVisible = visible;
        if (moved)
        {
            OnPropertyChanged(nameof(Page));
            OnPropertyChanged(nameof(PageCount));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
    }
}
