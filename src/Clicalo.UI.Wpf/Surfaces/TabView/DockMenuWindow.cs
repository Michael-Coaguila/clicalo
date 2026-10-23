using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The menu of a shortcut of the bar or of «Pinned» (CUA-014, PES-010): the same menu as the panel's
/// (<see cref="TileContextMenuView"/> over the same <see cref="TileContextMenuViewModel"/>: [ctxPin]/[ctxUnpin],
/// [ctxHide], [edit], [cancel], rows of 44), in a <see cref="NonActivatingWindow"/> beside the bar, never a
/// <c>ContextMenu</c> nor a <c>Popup</c> (REG-01). A long press, a right click or the accessible secondary action on a
/// shortcut opens it; a row, [cancel], a tap on another shortcut or button of the bar, Esc or a touch on another app
/// closes it (<c>MenuOutsideCancel</c>: the window never has the keyboard).
/// </summary>
public sealed class DockMenuWindow : TouchSurface
{
    // As wide as the guide beside the bar: at 230 «Remove from Frequents» ended in «…» (D21 review).
    private const double MenuWidth = 260;

    private readonly TileContextMenuViewModel _viewModel;
    private readonly TileContextMenuView _view;

    /// <summary>Creates the window on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">The menu, shared with the panel.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public DockMenuWindow(
        TileContextMenuViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.Menu, 1),
            registry,
            time,
            theme,
            touch,
            static (_, _, _) => { },
            SurfaceLook.SideWindow
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        // A side window like the others (D12): accent says «interactive or chosen», not «menu».
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        // The window is the surface (its background, its line and Space.S3 inside, as the windows beside the bar) and
        // the menu draws only its rows: no box of its own over the line of the window (D12).
        _view = new TileContextMenuView(viewModel)
        {
            Margin = new Thickness(0),
            Padding = new Thickness(Space.S3),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
        };
        Width = MenuWidth;
        FixedWidth = MenuWidth;
        SizeToContent = SizeToContent.Height;
        Content = _view;
        viewModel.PropertyChanged += OnChanged;
        Title = viewModel.AccessibleName;
    }

    /// <summary>The menu.</summary>
    public TileContextMenuView Menu => _view;

    /// <summary>The shortcut whose menu is open, or null: the menu opens at its height (CUA-014, D21 review).</summary>
    public ShortcutId? Shortcut => _viewModel.Shortcut;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        foreach (var target in _view.TapTargets)
        {
            yield return SurfaceTarget.Button(target.Element, target.Tap);
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _view.Detach();
        base.OnClosed(e);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        Title = _viewModel.AccessibleName;
        RefreshTargets();
    }
}
