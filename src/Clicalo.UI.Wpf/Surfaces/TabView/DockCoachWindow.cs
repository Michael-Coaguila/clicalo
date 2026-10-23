using System.ComponentModel;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The first-time guide of the Tab view (PES-015) in direction C (D12): a <see cref="CoachWindow"/> beside the bar,
/// aligned with its top, named [dockCoachName] in every step.
/// </summary>
public sealed class DockCoachWindow : CoachWindow
{
    private readonly DockBarViewModel _viewModel;

    /// <summary>Creates the guide on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">The bar: its guide texts and its intents.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public DockCoachWindow(
        DockBarViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.SideWindow, 10),
            registry,
            time,
            theme,
            touch,
            (viewModel ?? throw new ArgumentNullException(nameof(viewModel))).CoachNext,
            viewModel.CoachSkip
        )
    {
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Labels.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Labels.PropertyChanged -= OnChanged;
        base.OnClosed(e);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh() =>
        Show(
            new CoachContent(
                _viewModel.Labels.CoachName,
                _viewModel.CoachStepLabel,
                _viewModel.CoachTitle,
                _viewModel.CoachText,
                _viewModel.CoachNextLabel,
                _viewModel.Labels.CoachSkip,
                _viewModel.CoachSteps
            )
        );
}
