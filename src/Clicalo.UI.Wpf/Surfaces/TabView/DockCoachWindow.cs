using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The first-time guide of the Tab view (PES-015) in direction C (D12): a side window of 260 beside the bar, aligned
/// with its top, with «i / 3» as its <see cref="Overline"/>, the title (<see cref="TextStyleToken.Heading"/>) and the
/// text of the step (<see cref="TextStyleToken.Body"/>), and two buttons of 44 one under the other, so neither label
/// is cut: [next] ([understood] on the last step), the primary one, and [coachSkip], tertiary. The ★ of the texts is
/// drawn as the star icon of the bar, and read as the text says it. The window keeps the size of its longest step, so
/// it never changes size from one step to the next. Its texts are a polite live region.
/// </summary>
public sealed class DockCoachWindow : TouchSurface
{
    private const double CardWidth = 260;
    private const string Star = "★";

    private readonly DockBarViewModel _viewModel;
    private readonly Overline _step;
    private readonly StyledText _title;
    private readonly StyledText _text;
    private readonly StackPanel _texts;
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
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Width = CardWidth;
        FixedWidth = CardWidth;
        SizeToContent = SizeToContent.Height;
        _step = new Overline();
        _title = new StyledText(string.Empty, TextStyleToken.Heading)
        {
            MaxLines = 2,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        _text = new StyledText(string.Empty, TextStyleToken.Body)
        {
            MaxLines = 0,
            Margin = new Thickness(0, Space.S2, 0, 0),
        };
        AutomationProperties.SetLiveSetting(_text, AutomationLiveSetting.Polite);
        _texts = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        _ = _texts.Children.Add(_title);
        _ = _texts.Children.Add(_text);
        _next = new TouchButton
        {
            Variant = ButtonVariant.Primary,
            Focusable = false,
            IsTabStop = false,
            Margin = new Thickness(0, Space.S4, 0, Space.S1),
        };
        _next.Click += (_, _) => viewModel.CoachNext();
        _skip = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
            Focusable = false,
            IsTabStop = false,
        };
        _skip.Click += (_, _) => viewModel.CoachSkip();
        var stack = new StackPanel { Margin = new Thickness(Space.S4) };
        _ = stack.Children.Add(_step);
        _ = stack.Children.Add(_texts);
        _ = stack.Children.Add(_next);
        _ = stack.Children.Add(_skip);
        Content = stack;
        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Labels.PropertyChanged += OnChanged;

        // Measured again once the window has its fonts and its theme.
        Loaded += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>[coachSkip].</summary>
    public TouchButton SkipButton => _skip;

    /// <summary>[next] or [understood].</summary>
    public TouchButton NextButton => _next;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [
            SurfaceTarget.Button(_next, _viewModel.CoachNext),
            SurfaceTarget.Button(_skip, _viewModel.CoachSkip),
        ];

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Labels.PropertyChanged -= OnChanged;
        base.OnClosed(e);
    }

    /// <summary>
    /// Writes <paramref name="text"/> into <paramref name="block"/> with each ★ drawn as the star icon of the bar
    /// (a character of the font is not the icon the bar shows); UI Automation reads the text as written.
    /// </summary>
    private static void Write(TextBlock block, string text)
    {
        block.Inlines.Clear();
        var parts = text.Split(Star);
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                var star = new SymbolIcon { Symbol = "star", Size = IconBox.Small };
                star.SetResourceReference(
                    SymbolIcon.ForegroundProperty,
                    ThemeBrushKey.For(ColorToken.AccentInk)
                );
                block.Inlines.Add(
                    new InlineUIContainer(star) { BaselineAlignment = BaselineAlignment.Center }
                );
            }

            if (parts[i].Length > 0)
            {
                block.Inlines.Add(new Run(parts[i]));
            }
        }

        AutomationProperties.SetName(block, text);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        _step.Text = _viewModel.CoachStepLabel;
        Title = _viewModel.Labels.CoachName;
        _next.Content = _viewModel.CoachNextLabel;
        AutomationProperties.SetName(_next, _viewModel.CoachNextLabel);
        _skip.Content = _viewModel.Labels.CoachSkip;
        AutomationProperties.SetName(_skip, _viewModel.Labels.CoachSkip);
        KeepLongestStep();
        _title.Text = _viewModel.CoachTitle;
        Write(_text, _viewModel.CoachText);
    }

    /// <summary>
    /// Reserves the height of the longest step for the title and the text (D12: the window never changes size while
    /// the person goes through the steps).
    /// </summary>
    private void KeepLongestStep()
    {
        var border = BorderThickness;
        var width = Math.Max(0, CardWidth - (2 * Space.S4) - border.Left - border.Right);
        var tallest = 0d;
        foreach (var (title, text) in _viewModel.CoachSteps)
        {
            _title.Text = title;
            Write(_text, text);

            // The texts themselves: their new text marks them for measure, the panel around them is not yet.
            var room = new Size(width, double.PositiveInfinity);
            _title.Measure(room);
            _text.Measure(room);
            tallest = Math.Max(tallest, _title.DesiredSize.Height + _text.DesiredSize.Height);
        }

        _texts.MinHeight = tallest;
    }
}
