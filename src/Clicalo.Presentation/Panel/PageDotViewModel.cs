using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// One page dot (CUA-004): 24 wide when active and 10 otherwise, named «[pageN] n», with a 44 touch target; a tap
/// jumps to its page.
/// </summary>
public sealed class PageDotViewModel : ObservableObject
{
    private readonly Action<int> _goTo;
    private bool _isActive;
    private double _widthPx;
    private string _accessibleName = string.Empty;

    internal PageDotViewModel(int page, Action<int> goTo)
    {
        Page = page;
        _goTo = goTo;
    }

    /// <summary>The page it jumps to, from 0.</summary>
    public int Page { get; }

    /// <summary>Whether it is the page in view.</summary>
    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    /// <summary>Visual width in device-independent pixels.</summary>
    public double WidthPx
    {
        get => _widthPx;
        private set => SetProperty(ref _widthPx, value);
    }

    /// <summary>«Página n».</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>A tap or UI Automation Invoke: show this page.</summary>
    public void Select() => _goTo(Page);

    internal void Apply(bool active, double widthPx, string name)
    {
        IsActive = active;
        WidthPx = widthPx;
        AccessibleName = name;
    }
}
