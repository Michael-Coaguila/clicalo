using System.ComponentModel;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Guides;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// The window of the guides of the panel, the bar and the bubble (PES-015, R-39): a <see cref="GuideCard"/> in a side
/// window of 260 that never takes the focus (REG-01), never a popup, named as its guide in every step. The surface set
/// places it beside the window of the control the step points at, so it never covers it.
/// </summary>
public sealed class GuideWindow : TouchSurface
{
    /// <summary>Its instance among the side windows.</summary>
    public const int Instance = 10;

    private readonly GuideViewModel _viewModel;
    private readonly GuideCard _card;

    /// <summary>Creates the window on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="viewModel">The guide on show.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public GuideWindow(
        GuideViewModel viewModel,
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
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Width = GuideCard.CardWidth;
        FixedWidth = GuideCard.CardWidth;
        SizeToContent = SizeToContent.Height;
        _card = new GuideCard(viewModel, focusable: false, () => BorderThickness);
        Content = _card;
        Title = viewModel.Name;
        _viewModel.PropertyChanged += OnChanged;
    }

    /// <summary>The card.</summary>
    public GuideCard Card => _card;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [
            SurfaceTarget.Button(_card.NextButton, _viewModel.Next),
            SurfaceTarget.Button(_card.SkipButton, _viewModel.Skip),
        ];

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _card.Detach();
        base.OnClosed(e);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(GuideViewModel.Name), StringComparison.Ordinal))
        {
            Title = _viewModel.Name;
        }
    }
}
