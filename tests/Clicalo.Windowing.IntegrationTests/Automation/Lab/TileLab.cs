using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// The content of the S3 lab panel: the nine <see cref="LabTiles"/> in a 3 × 3 grid, a notice bar with a
/// <see cref="LiveAnnouncer"/> and the smallest view model behavior (a toggle cycles its states, expand and
/// collapse set the state), under a <see cref="ThemeScope"/>. Create and use it on the WPF thread.
/// </summary>
public sealed class TileLab : IDisposable
{
    /// <summary>Automation id of the notice bar.</summary>
    public const string NoticeId = "notice";

    /// <summary>The idle text of the notice bar (AVI-001 [ready]).</summary>
    public const string IdleNotice = "Listo. Toca un botón para usarlo.";

    /// <summary>Logical size of a tile (size M of <c>sizes.json</c>).</summary>
    public static readonly Size TileSize = new(92, 78);

    private readonly Dictionary<string, ShortcutTile> _tiles = new(StringComparer.Ordinal);

    /// <summary>Builds the lab with <paramref name="theme"/> preferred.</summary>
    public TileLab(ThemeId theme = ThemeId.Dark)
    {
        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(8) };
        foreach (var spec in LabTiles.All)
        {
            var tile = new ShortcutTile
            {
                AccessibleName = spec.Name,
                AccessibleHelpText = spec.HelpText,
                Pattern = spec.Pattern,
                Width = TileSize.Width,
                Height = TileSize.Height,
                Margin = new Thickness(4),
            };
            AutomationProperties.SetAutomationId(tile, spec.Id);
            Wire(tile, spec);
            _tiles.Add(spec.Id, tile);
            grid.Children.Add(tile);
        }

        Notice = new TextBlock
        {
            Text = IdleNotice,
            Margin = new Thickness(12, 4, 12, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(Notice, NoticeId);
        Notice.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(UI.Wpf.Theming.Generated.ColorToken.Text)
        );
        DockPanel.SetDock(Notice, Dock.Bottom);

        Root = new DockPanel { LastChildFill = true };
        Root.SetResourceReference(
            Panel.BackgroundProperty,
            ThemeBrushKey.For(UI.Wpf.Theming.Generated.ColorToken.Win)
        );
        Root.Children.Add(Notice);
        Root.Children.Add(grid);
        Theme = new ThemeScope(Root, theme);
        Announcer = new LiveAnnouncer(Notice);
    }

    /// <summary>The root element: the surface content.</summary>
    public DockPanel Root { get; }

    /// <summary>The notice bar (the live region).</summary>
    public TextBlock Notice { get; }

    /// <summary>The announcer of the notice bar.</summary>
    public LiveAnnouncer Announcer { get; }

    /// <summary>The theme of the lab.</summary>
    public ThemeScope Theme { get; }

    /// <summary>Every tile event, in order.</summary>
    public EventLog<LabInvocation> Log { get; } = new();

    /// <summary>The tiles, in panel order.</summary>
    public IEnumerable<ShortcutTile> Tiles => LabTiles.All.Select(spec => _tiles[spec.Id]);

    /// <summary>The tile with <paramref name="id"/>.</summary>
    public ShortcutTile Tile(string id) => _tiles[id];

    /// <summary>Turns «Numbers for voice» on (tile N gets N) or off.</summary>
    public void SetVoiceNumbers(bool on)
    {
        var number = 1;
        foreach (var tile in Tiles)
        {
            tile.VoiceNumber = on ? number : null;
            number++;
        }
    }

    /// <summary>
    /// Back to the initial state of the view model: voice numbers off, toggles off, profile collapsed, the idle
    /// notice in a polite region. Each desktop test starts from here.
    /// </summary>
    public void Reset()
    {
        SetVoiceNumbers(false);
        foreach (var tile in Tiles)
        {
            tile.ToggleState = ToggleState.Off;
            tile.AccessibleState = string.Empty;
            tile.IsExpanded = false;
        }

        Notice.Text = IdleNotice;
        AutomationProperties.SetLiveSetting(Notice, AutomationLiveSetting.Polite);
    }

    /// <summary>Lays the content out at <paramref name="size"/> without a window (headless tests).</summary>
    public void LayOut(Size size)
    {
        Root.Measure(size);
        Root.Arrange(new Rect(size));
        Root.UpdateLayout();
    }

    /// <inheritdoc />
    public void Dispose() => Theme.Dispose();

    private static void Cycle(ShortcutTile tile, LabTileSpec spec)
    {
        tile.ToggleState = (tile.ToggleState, spec.ThreeStates) switch
        {
            (ToggleState.Off, _) => ToggleState.On,
            (ToggleState.On, true) => ToggleState.Indeterminate,
            _ => ToggleState.Off,
        };
        tile.AccessibleState = tile.ToggleState switch
        {
            ToggleState.On => LabTiles.OnState,
            ToggleState.Indeterminate => LabTiles.LockedState,
            _ => string.Empty,
        };
    }

    private void Wire(ShortcutTile tile, LabTileSpec spec)
    {
        tile.Invoked += (_, _) => Record(spec, LabInvocationKind.Invoked);
        tile.Toggled += (_, _) =>
        {
            Cycle(tile, spec);
            Record(spec, LabInvocationKind.Toggled);
        };
        tile.ExpandRequested += (_, _) =>
        {
            tile.IsExpanded = true;
            Record(spec, LabInvocationKind.ExpandRequested);
        };
        tile.CollapseRequested += (_, _) =>
        {
            tile.IsExpanded = false;
            Record(spec, LabInvocationKind.CollapseRequested);
        };
    }

    private void Record(LabTileSpec spec, LabInvocationKind kind) =>
        Log.Record(new LabInvocation(spec.Id, kind, Environment.CurrentManagedThreadId));
}
