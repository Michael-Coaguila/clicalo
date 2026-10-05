using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The notice bar (AVI-001, AVI-003, AVI-004): a strip at the foot, radius 10, at least 40 high (34 in Compact), 13 px
/// text announced as a polite live region. At rest, the info icon in muted and [ready] on a transparent fill; a notice
/// on card with its icon in accent; a warning on warnWash with its icon in warn. [undo] in accent when the notice can
/// be undone, and ↻ (36 × 32 visual, 44 touch) when there is a last action. It only projects
/// <see cref="NoticeBarViewModel"/>.
/// </summary>
public sealed class NoticeBarView : Border
{
    private const double IconPx = 18;
    private const double TextPx = 13;
    private const double ButtonHeight = 32;
    private const double RepeatWidth = 36;
    private const double Gap = 8;
    private const double CompactHeight = 34;

    private readonly NoticeBarViewModel _viewModel;
    private readonly SymbolIcon _icon = new()
    {
        Size = IconPx,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _text = new()
    {
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(Gap, 0, Gap, 0),
    };

    private readonly TouchButton _undo = new()
    {
        Appearance = ButtonAppearance.Accent,
        Height = ButtonHeight,
        Padding = new Thickness(10, 0, 10, 0),
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, Gap, 0),
    };

    private readonly IconButton _repeat = new()
    {
        Symbol = "replay",
        IconSize = IconPx,
        Width = RepeatWidth,
        Height = ButtonHeight,
        Appearance = ButtonAppearance.Neutral,
        Focusable = false,
        IsTabStop = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly LiveAnnouncer _announcer;
    private string _announced = string.Empty;

    /// <summary>Creates the bar of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The bar.</param>
    /// <param name="compact">The Compact view (34 high, VCO-001).</param>
    public NoticeBarView(NoticeBarViewModel viewModel, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        CornerRadius = new CornerRadius(Radii.Button);
        Padding = new Thickness(10, 0, 10, 0);
        Margin = compact ? new Thickness(10, 8, 10, 10) : new Thickness(12, 10, 12, 12);
        MinHeight = compact ? CompactHeight : PanelSizes.Layout.PanelNoticeBarHeightPx;
        _text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _undo.SetResourceReference(Control.FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _undo.Click += (_, _) => _viewModel.Undo();
        _repeat.Click += (_, _) => _viewModel.Repeat();

        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_icon, Dock.Left);
        DockPanel.SetDock(_repeat, Dock.Right);
        DockPanel.SetDock(_undo, Dock.Right);
        row.Children.Add(_icon);
        row.Children.Add(_repeat);
        row.Children.Add(_undo);
        row.Children.Add(_text);
        Child = row;
        _announcer = new LiveAnnouncer(_text);

        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>[undo] and ↻ while they show.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (!_viewModel.IsVisible)
            {
                yield break;
            }

            if (_viewModel.CanUndo)
            {
                yield return new PanelTapTarget(_undo, _viewModel.Undo);
            }

            if (_viewModel.CanRepeat)
            {
                yield return new PanelTapTarget(_repeat, _viewModel.Repeat);
            }
        }
    }

    /// <summary>The text of the bar (the live region).</summary>
    public TextBlock MessageText => _text;

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _icon.Symbol = _viewModel.Icon;
        var (background, tint, text) = _viewModel.Tone switch
        {
            NoticeTone.Notice => ((ColorToken?)ColorToken.Card, ColorToken.Accent, ColorToken.Text),
            NoticeTone.Warning => (ColorToken.WarnWash, ColorToken.Warn, ColorToken.Text),
            _ => (null, ColorToken.Muted, ColorToken.Muted),
        };
        PanelChrome.SetBrush(this, BackgroundProperty, background);
        _icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(tint));
        _text.SetResourceReference(TextBlock.ForegroundProperty, ThemeBrushKey.For(text));
        _undo.Content = _viewModel.UndoName;
        _undo.Visibility = _viewModel.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(_repeat, _viewModel.RepeatName);
        _repeat.Visibility = _viewModel.CanRepeat ? Visibility.Visible : Visibility.Collapsed;
        Say(_viewModel.Message, _viewModel.Tone);
    }

    /// <summary>
    /// Shows the message; a notice is announced once as a polite live region (AVI-001), the text at rest is only
    /// shown (silence is never announced).
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

        _announcer.Announce(message, AnnouncementUrgency.Polite);
    }
}
