using System.ComponentModel;
using System.Windows;
using Clicalo.Application.Ports;
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
/// shortcut opens it; a row, [cancel] or a tap on another shortcut closes it.
/// </summary>
public sealed class DockMenuWindow : TouchSurface
{
    private const double MenuWidth = 230;

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
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Accent));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        _view = new TileContextMenuView(viewModel)
        {
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
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
