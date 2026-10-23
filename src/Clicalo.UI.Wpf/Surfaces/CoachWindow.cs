using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// A first-time guide in direction C (D12), the same beside the bar of the Tab view (PES-015) and beside the panel
/// (R-39): a side window of 260 with «i / 3» as its <see cref="Overline"/>, the title (<see cref="TextStyleToken.Heading"/>)
/// and the text of the step (<see cref="TextStyleToken.Body"/>), and two buttons of 44 one under the other, so neither
/// label is cut: [next] ([understood] on the last step), the primary one, and [coachSkip], tertiary. The ★ of the texts
/// is drawn as the star icon of the bar, and read as the text says it. The window keeps the size of its longest step, so
/// it never changes size from one step to the next. Its texts are a polite live region.
/// </summary>
public abstract class CoachWindow : TouchSurface
{
    /// <summary>The width of the window, as PES-015 sets it.</summary>
    public const double CardWidth = 260;

    private const string Star = "★";

    private readonly Action _next;
    private readonly Action _skip;
    private readonly Overline _step;
    private readonly StyledText _title;
    private readonly StyledText _text;
    private readonly LiveAnnouncer _announcer;
    private readonly StackPanel _texts;
    private readonly TouchButton _skipButton;
    private readonly TouchButton _nextButton;
    private CoachContent? _content;
    private string _said = string.Empty;

    /// <summary>Creates the guide on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="id">Its identity in the registry.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    /// <param name="next">[next], or [understood] on the last step.</param>
    /// <param name="skip">[coachSkip].</param>
    protected CoachWindow(
        SurfaceId id,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch,
        Action next,
        Action skip
    )
        : base(id, registry, time, theme, touch, static (_, _, _) => { }, SurfaceLook.SideWindow)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(skip);
        _next = next;
        _skip = skip;
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
        _announcer = new LiveAnnouncer(_text);
        _texts = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        _ = _texts.Children.Add(_title);
        _ = _texts.Children.Add(_text);
        _nextButton = new TouchButton
        {
            Variant = ButtonVariant.Primary,
            Focusable = false,
            IsTabStop = false,
            Margin = new Thickness(0, Space.S4, 0, Space.S1),
        };
        _nextButton.Click += (_, _) => next();
        _skipButton = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
            Focusable = false,
            IsTabStop = false,
        };
        _skipButton.Click += (_, _) => skip();
        var stack = new StackPanel { Margin = new Thickness(Space.S4) };
        _ = stack.Children.Add(_step);
        _ = stack.Children.Add(_texts);
        _ = stack.Children.Add(_nextButton);
        _ = stack.Children.Add(_skipButton);
        Content = stack;

        // Measured again once the window has its fonts and its theme.
        Loaded += (_, _) => Redraw();
        IsVisibleChanged += (_, _) => Say();
    }

    /// <summary>[coachSkip].</summary>
    public TouchButton SkipButton => _skipButton;

    /// <summary>[next] or [understood].</summary>
    public TouchButton NextButton => _nextButton;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [SurfaceTarget.Button(_nextButton, _next), SurfaceTarget.Button(_skipButton, _skip)];

    /// <summary>Shows <paramref name="content"/>, and says the step if it changed.</summary>
    /// <param name="content">The step on show.</param>
    protected void Show(CoachContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
        Redraw();
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

    private void Redraw()
    {
        if (_content is not { } content)
        {
            return;
        }

        _step.Text = content.StepLabel;
        Title = content.Name;
        _nextButton.Content = content.NextLabel;
        AutomationProperties.SetName(_nextButton, content.NextLabel);
        _skipButton.Content = content.SkipLabel;
        AutomationProperties.SetName(_skipButton, content.SkipLabel);
        KeepLongestStep(content.Steps);
        _title.Text = content.Title;
        Write(_text, content.Text);
        Say();
    }

    /// <summary>Says the step once when it changes and when the guide appears (ACC-001): the region was never raised.</summary>
    private void Say()
    {
        if (!IsVisible || _content is not { Title.Length: > 0 } content)
        {
            _said = string.Empty;
            return;
        }

        var step = content.Title + Environment.NewLine + content.Text;
        if (!string.Equals(step, _said, StringComparison.Ordinal))
        {
            _said = step;
            _announcer.Say(step, AnnouncementUrgency.Polite);
        }
    }

    /// <summary>
    /// Reserves the height of the longest step for the title and the text (D12: the window never changes size while
    /// the person goes through the steps).
    /// </summary>
    private void KeepLongestStep(IReadOnlyList<(string Title, string Text)> steps)
    {
        var border = BorderThickness;
        var width = Math.Max(0, CardWidth - (2 * Space.S4) - border.Left - border.Right);
        var tallest = 0d;
        foreach (var (title, text) in steps)
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
