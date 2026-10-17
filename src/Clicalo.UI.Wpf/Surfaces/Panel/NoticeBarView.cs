using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The status row at the foot of the panel (AVI-001, AVI-003, AVI-004) in direction C (D12): a row of
/// <see cref="ControlHeight.Small"/> on the surface of the panel with the decorative line above it, as wide as the
/// panel and down to its rounded foot, always there (the panel has the size of its design). The message is one line with «…» in
/// <c>bodySmall</c>, announced as a polite live region (assertive for a warning). A confirmation shows ✓ in
/// <c>success</c>, never the information icon; another notice shows its own icon in <c>muted</c>, a warning ⚠ in
/// <c>warnText</c>, and the rest says [ready] with no icon. At its end, [cancel] and ↶ [undo] as tertiary buttons and ↻
/// [repeat] as an icon button of 44 × 44: the same actions with the same look as the notice window of the Tab view and
/// the status bar of the Control Center. It only projects <see cref="NoticeBarViewModel"/>.
/// </summary>
public sealed class NoticeBarView : Border
{
    /// <summary>The icon the composition gives a plain notice: in the panel it is a confirmation, so it shows ✓.</summary>
    private const string InfoSymbol = "info";

    private const string ConfirmSymbol = "check_circle";

    private readonly NoticeBarViewModel _viewModel;
    private readonly SymbolIcon _icon = PanelChrome.Icon(null, IconBox.Small, ColorToken.Muted);
    private readonly StyledText _text = PanelChrome.Text(TextStyleToken.BodySmall);

    private readonly TouchButton _undo = new()
    {
        Variant = ButtonVariant.Tertiary,
        Symbol = "undo",
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TouchButton _cancel = new()
    {
        Variant = ButtonVariant.Tertiary,
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly IconButton _repeat = new()
    {
        Symbol = "replay",
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly LiveAnnouncer _announcer;
    private string _announced = string.Empty;

    /// <summary>Creates the row of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The notices.</param>
    public NoticeBarView(NoticeBarViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        BorderThickness = new Thickness(0, Stroke.Line, 0, 0);
        Padding = new Thickness(Space.S3, 0, Space.S1, Space.S3);
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        _icon.Margin = new Thickness(0, 0, Space.S2, 0);
        _text.VerticalAlignment = VerticalAlignment.Center;
        _undo.Click += (_, _) => _viewModel.Undo();
        _cancel.Click += (_, _) => _viewModel.Cancel();
        _repeat.Click += (_, _) => _viewModel.Repeat();

        var row = new DockPanel { LastChildFill = true, Height = ControlHeight.Small };
        DockPanel.SetDock(_icon, Dock.Left);
        DockPanel.SetDock(_repeat, Dock.Right);
        DockPanel.SetDock(_undo, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        row.Children.Add(_icon);
        row.Children.Add(_repeat);
        row.Children.Add(_undo);
        row.Children.Add(_cancel);
        row.Children.Add(_text);
        Child = row;
        _announcer = new LiveAnnouncer(_text);

        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>[undo], ↻ and [cancel] while they show.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (_viewModel.CanUndo)
            {
                yield return new PanelTapTarget(_undo, _viewModel.Undo);
            }

            if (_viewModel.CanRepeat)
            {
                yield return new PanelTapTarget(_repeat, _viewModel.Repeat);
            }

            if (_viewModel.CanCancel)
            {
                yield return new PanelTapTarget(_cancel, _viewModel.Cancel);
            }
        }
    }

    /// <summary>The text of the row (the live region).</summary>
    public TextBlock MessageText => _text;

    /// <summary>The icon of the message (✓ for a confirmation).</summary>
    public SymbolIcon Icon => _icon;

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var (symbol, ink) = _viewModel.Tone switch
        {
            NoticeTone.Warning => (_viewModel.Icon, ColorToken.WarnText),
            NoticeTone.Notice when _viewModel.Icon is InfoSymbol or ConfirmSymbol => (
                ConfirmSymbol,
                ColorToken.Success
            ),
            NoticeTone.Notice => (_viewModel.Icon, ColorToken.Muted),
            _ => ((string?)null, ColorToken.Muted),
        };
        _icon.Symbol = string.IsNullOrEmpty(symbol) ? null : symbol;
        _icon.Visibility = _icon.Symbol is null ? Visibility.Collapsed : Visibility.Visible;
        _icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(ink));
        _text.Ink = _viewModel.Tone == NoticeTone.Rest ? ColorToken.Muted : ColorToken.Text;
        _undo.Content = _viewModel.UndoName;
        _undo.Visibility = _viewModel.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        _cancel.Content = _viewModel.CancelName;
        _cancel.Visibility = _viewModel.CanCancel ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(_repeat, _viewModel.RepeatName);
        _repeat.Visibility = _viewModel.CanRepeat ? Visibility.Visible : Visibility.Collapsed;
        Say(_viewModel.Message, _viewModel.Tone);
    }

    /// <summary>
    /// Shows the message; a notice is announced once as a live region (AVI-001, polite; a warning assertive), the text at
    /// rest is only shown (silence is never announced).
    /// </summary>
    private void Say(string message, NoticeTone tone)
    {
        if (string.Equals(message, _announced, StringComparison.Ordinal))
        {
            return;
        }

        _announced = message;
        if (tone == NoticeTone.Rest || PresentationSource.FromVisual(_text) is null)
        {
            _text.Text = message;
            return;
        }

        // Errors and safety notices are assertive (ACC-001); confirmations polite.
        _announcer.Announce(
            message,
            tone == NoticeTone.Warning ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite
        );
    }
}
