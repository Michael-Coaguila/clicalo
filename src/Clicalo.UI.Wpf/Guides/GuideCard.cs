using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.Presentation.Guides;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// The card of a first-time guide in direction C (D12), the same beside the panel, the bar and the bubble and inside the
/// Control Center (PES-015, R-39): «i / n» as its <see cref="Overline"/>, the title (<see cref="TextStyleToken.Heading"/>)
/// and the text of the step (<see cref="TextStyleToken.Body"/>), and two buttons of 44 one under the other, so neither
/// label is cut: [next] ([understood] on the last step), the primary one, and [skip], tertiary. The ★ of the texts is
/// drawn as the star icon of the panel, and read as the text says it. It keeps the height of the longest step of its
/// guide, so it never changes size from one step to the next. Its texts are a polite live region: each step is said
/// once when it changes and when the card appears (ACC-001).
/// </summary>
public sealed class GuideCard : StackPanel
{
    /// <summary>The width of the card with its line, as PES-015 sets it.</summary>
    public const double CardWidth = 260;

    private const string Star = "★";

    private readonly GuideViewModel _viewModel;
    private readonly Func<Thickness> _line;
    private readonly Overline _step;
    private readonly StyledText _title;
    private readonly StyledText _text;
    private readonly LiveAnnouncer _announcer;
    private readonly StackPanel _texts;
    private readonly TouchButton _next;
    private readonly TouchButton _skip;
    private string _said = string.Empty;

    /// <summary>Creates the card of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The guide.</param>
    /// <param name="focusable">
    /// Whether its buttons take the keyboard: inside the Control Center they do; beside the panel nothing ever does
    /// (REG-01).
    /// </param>
    /// <param name="line">The line around the card, which narrows its texts.</param>
    public GuideCard(GuideViewModel viewModel, bool focusable, Func<Thickness> line)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(line);
        _viewModel = viewModel;
        _line = line;
        Margin = new Thickness(Space.S4);
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
        _next = new TouchButton
        {
            Variant = ButtonVariant.Primary,
            Focusable = focusable,
            IsTabStop = focusable,
            Margin = new Thickness(0, Space.S4, 0, Space.S1),
        };
        _next.Click += (_, _) => viewModel.Next();
        _skip = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
            Focusable = focusable,
            IsTabStop = focusable,
        };
        _skip.Click += (_, _) => viewModel.Skip();
        _ = Children.Add(_step);
        _ = Children.Add(_texts);
        _ = Children.Add(_next);
        _ = Children.Add(_skip);
        _viewModel.PropertyChanged += OnChanged;

        // Measured again once the card has its fonts and its theme.
        Loaded += (_, _) => Redraw();
        IsVisibleChanged += (_, _) => Say();
        Redraw();
    }

    /// <summary>[next] or [understood].</summary>
    public TouchButton NextButton => _next;

    /// <summary>[skip].</summary>
    public TouchButton SkipButton => _skip;

    /// <summary>Stops following the guide (its window or its section is gone).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    /// <summary>
    /// Writes <paramref name="text"/> into <paramref name="block"/> with each ★ drawn as the star icon of the panel
    /// (a character of the font is not the icon the panel shows); UI Automation reads the text as written.
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

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The steps change together; the text, set last but one, carries the redraw and the target follows it.
        if (
            e.PropertyName
            is nameof(GuideViewModel.Text)
                or nameof(GuideViewModel.StepLabel)
                or nameof(GuideViewModel.NextLabel)
                or nameof(GuideViewModel.SkipLabel)
                or nameof(GuideViewModel.Steps)
                or nameof(GuideViewModel.IsShown)
        )
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        if (!_viewModel.IsShown)
        {
            _said = string.Empty;
            return;
        }

        _step.Text = _viewModel.StepLabel;
        _next.Content = _viewModel.NextLabel;
        AutomationProperties.SetName(_next, _viewModel.NextLabel);
        _skip.Content = _viewModel.SkipLabel;
        AutomationProperties.SetName(_skip, _viewModel.SkipLabel);
        KeepLongestStep();
        _title.Text = _viewModel.Title;
        Write(_text, _viewModel.Text);
        Say();
    }

    /// <summary>Says the step once when it changes and when the card appears (ACC-001): the region was never raised.</summary>
    private void Say()
    {
        if (!IsVisible || !_viewModel.IsShown || _viewModel.Title.Length == 0)
        {
            _said = string.Empty;
            return;
        }

        var step = _viewModel.Title + Environment.NewLine + _viewModel.Text;
        if (!string.Equals(step, _said, StringComparison.Ordinal))
        {
            _said = step;
            _announcer.Say(step, AnnouncementUrgency.Polite);
        }
    }

    /// <summary>
    /// Reserves the height of the longest step of the guide for the title and the text (D12: the card never changes size
    /// while the person goes through the steps).
    /// </summary>
    private void KeepLongestStep()
    {
        var line = _line();
        var width = Math.Max(0, CardWidth - (2 * Space.S4) - line.Left - line.Right);
        var tallest = 0d;
        foreach (var (title, text) in _viewModel.Steps)
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
