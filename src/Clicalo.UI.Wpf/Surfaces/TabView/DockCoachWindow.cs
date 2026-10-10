using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using TouchTargetSize = Clicalo.UI.Wpf.Controls.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The first-time guide of the Tab view (PES-015): an <c>accent</c> card of 260 beside the bar, aligned with its top,
/// with «i / 3», the title and the text of the step and two buttons of 44: [coachSkip] and [next] ([understood] on the
/// last step). Its texts are a polite live region.
/// </summary>
public sealed class DockCoachWindow : TouchSurface
{
    private const double CardWidth = 260;
    private const double StepPx = 12;
    private const double TitlePx = 15;
    private const double TextPx = 13;

    private readonly DockBarViewModel _viewModel;
    private readonly TextBlock _step;
    private readonly TextBlock _title;
    private readonly TextBlock _text;
    private readonly TouchButton _skip;
    private readonly TouchButton _next;

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
            static (_, _, _) => { },
            SurfaceLook.SideWindow
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Accent));
        Width = CardWidth;
        FixedWidth = CardWidth;
        SizeToContent = SizeToContent.Height;
        _step = Wrapped(StepPx, bold: true);
        _step.Opacity = 0.85;
        _title = Wrapped(TitlePx, bold: true);
        _text = Wrapped(TextPx, bold: false);
        AutomationProperties.SetLiveSetting(_text, AutomationLiveSetting.Polite);
        _skip = SurfaceParts.Button(
            "close",
            0,
            ButtonAppearance.Outline,
            viewModel.CoachSkip,
            height: TouchTargetSize.MinimumSize
        );
        _skip.Symbol = null;
        _next = SurfaceParts.Button(
            "arrow_forward",
            0,
            ButtonAppearance.Neutral,
            viewModel.CoachNext,
            height: TouchTargetSize.MinimumSize
        );
        _next.Symbol = null;
        // On the accent card: [coachSkip] outlined in onAccent, [next] filled with onAccent in accent (PES-015).
        _skip.SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.OnAccent));
        _skip.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.OnAccent));
        _next.SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.OnAccent));
        _next.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.OnAccent));
        _next.SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Accent));
        var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 0) };
        _skip.Margin = new Thickness(0, 0, 3, 0);
        _next.Margin = new Thickness(3, 0, 0, 0);
        _ = buttons.Children.Add(_skip);
        _ = buttons.Children.Add(_next);
        var stack = new StackPanel { Margin = new Thickness(14) };
        foreach (var part in new FrameworkElement[] { _step, _title, _text, buttons })
        {
            part.Margin = new Thickness(0, 0, 0, 10);
            _ = stack.Children.Add(part);
        }

        Content = stack;
        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Labels.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>[coachSkip].</summary>
    public TouchButton SkipButton => _skip;

    /// <summary>[next] or [understood].</summary>
    public TouchButton NextButton => _next;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [
            SurfaceTarget.Button(_skip, _viewModel.CoachSkip),
            SurfaceTarget.Button(_next, _viewModel.CoachNext),
        ];

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Labels.PropertyChanged -= OnChanged;
        base.OnClosed(e);
    }

    private static TextBlock Wrapped(double px, bool bold)
    {
        var text = SurfaceParts.Text(px, ColorToken.OnAccent, bold);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        return text;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        _step.Text = _viewModel.CoachStepLabel;
        _title.Text = _viewModel.CoachTitle;
        _text.Text = _viewModel.CoachText;
        Title = _viewModel.CoachTitle;
        _skip.Content = _viewModel.Labels.CoachSkip;
        SurfaceParts.Name(_skip, _viewModel.Labels.CoachSkip);
        _next.Content = _viewModel.CoachNextLabel;
        SurfaceParts.Name(_next, _viewModel.CoachNextLabel);
    }
}
