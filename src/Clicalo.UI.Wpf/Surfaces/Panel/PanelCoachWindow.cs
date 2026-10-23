using System.ComponentModel;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The first-time guide of the Full and Compact views (R-38): a <see cref="CoachWindow"/> beside the panel, toward the
/// side of the screen with room and lined up with its top, as Quick settings are (AJR-001), named [panelCoachName] in
/// every step. A window of its own, never a popup, so it never takes the focus (REG-01).
/// </summary>
public sealed class PanelCoachWindow : CoachWindow
{
    /// <summary>Its instance among the side windows (the guide of the Tab view is 10).</summary>
    public const int Instance = 11;

    private readonly PanelCoachViewModel _viewModel;

    /// <summary>Creates the guide on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">The guide: its texts and its intents.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public PanelCoachWindow(
        PanelCoachViewModel viewModel,
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
            (viewModel ?? throw new ArgumentNullException(nameof(viewModel))).Next,
            viewModel.Skip
        )
    {
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        base.OnClosed(e);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Whether it shows is the surface set's to apply; the texts are this window's.
        if (
            !string.Equals(
                e.PropertyName,
                nameof(PanelCoachViewModel.IsShown),
                StringComparison.Ordinal
            )
        )
        {
            Refresh();
        }
    }

    private void Refresh() =>
        Show(
            new CoachContent(
                _viewModel.Name,
                _viewModel.StepLabel,
                _viewModel.Title,
                _viewModel.Text,
                _viewModel.NextLabel,
                _viewModel.SkipLabel,
                _viewModel.Steps
            )
        );
}
