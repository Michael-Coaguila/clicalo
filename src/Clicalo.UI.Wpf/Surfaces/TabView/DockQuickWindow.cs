using System.ComponentModel;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// Quick settings beside the open bar of the Tab view (PES-009, DIS-22): the same sheet as the panel's
/// (<see cref="QuickSettingsSheet"/> over the same <see cref="QuickSettingsViewModel"/>), with the row «Lado de la
/// pestaña» (AJR-005), in a <see cref="NonActivatingWindow"/> of its own, never a <c>Popup</c> (REG-01). The <c>tune</c>
/// button of the bar opens and closes it. It scrolls and slides the opacity with the finger, like the sheet of the
/// panel, and a contact that did so activates nothing (TAC-004).
/// </summary>
public sealed class DockQuickWindow : TouchSurface
{
    /// <summary>The instance of the side window in the registry, after the three windows of the bar.</summary>
    public const int Instance = 4;

    private const double SheetWidth = 288;

    private readonly QuickSettingsViewModel _viewModel;
    private readonly QuickSettingsSheet _sheet;

    /// <summary>Creates the window on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">Quick settings, shared with the panel.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public DockQuickWindow(
        QuickSettingsViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.SideWindow, Instance),
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
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        _sheet = new QuickSettingsSheet(viewModel)
        {
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        Width = SheetWidth;
        SizeToContent = SizeToContent.Height;
        Content = _sheet;
        viewModel.PropertyChanged += OnChanged;
        Title = viewModel.Name;
    }

    /// <summary>The sheet.</summary>
    public QuickSettingsSheet Sheet => _sheet;

    /// <summary>The tallest it may be, in logical pixels; what does not fit scrolls inside the sheet.</summary>
    /// <param name="height">The height.</param>
    public void FitHeight(double height) => _sheet.FitHeight(height);

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        foreach (var target in _sheet.TapTargets)
        {
            yield return SurfaceTarget.Button(target.Element, target.Tap);
        }
    }

    /// <inheritdoc />
    protected override bool TrackContact(in PointerSample sample) =>
        _sheet.Track(sample, DragThresholdPx);

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _sheet.Detach();
        base.OnClosed(e);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        Title = _viewModel.Name;
        RefreshTargets();
    }
}
