using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The notice surface of the Tab view (PES-014, DIS-45): a <see cref="NonActivatingWindow"/> beside the bar, or beside
/// the handle when the bar is closed, that tells what the notice bar tells in the Full view. From top to bottom: the
/// mark «Modo prueba · {s} s» while test mode is on (TAC-008); the administrator notice with its button (EJE-013); and
/// the notice on show (AVI-001, AVI-002) with [undo] or [cancel] when it offers them: the armed confirmation, why the
/// keys were released, what test mode saw, the capture of an app, a Mantener in course. It shows only while there is
/// something to tell, never dims and never takes the focus (REG-01). The notice is a live region like the bar's: polite,
/// or assertive for a warning, announced when it changes and when the surface appears.
/// </summary>
/// <remarks>
/// It projects the same view models as the panel (<see cref="NoticeBarViewModel"/>, <see cref="AdminNoticeViewModel"/>,
/// <see cref="TestModeViewModel"/>), so the Tab view can never tell something else than the panel would.
/// </remarks>
public sealed class DockNoticeWindow : TouchSurface
{
    /// <summary>The instance of the notice surface in the registry; the floating «Release all» is the first.</summary>
    public const int Instance = 2;

    private const double SurfaceWidth = 300;
    private const double IconPx = 18;
    private const double TextPx = 13;
    private const double ButtonHeight = 32;
    private const double Gap = 8;

    private readonly NoticeBarViewModel _notices;
    private readonly DockLabels _labels;
    private readonly AdminNoticeView _admin;
    private readonly TestModeIndicator? _testMode;
    private readonly Border _notice;
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
    private readonly TouchButton _undo = NoticeButton(ButtonAppearance.Accent);
    private readonly TouchButton _cancel = NoticeButton(ButtonAppearance.Neutral);
    private readonly LiveAnnouncer _announcer;
    private string _announced = string.Empty;

    /// <summary>Creates the surface on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="panel">The panel: its notice bar and its administrator notice.</param>
    /// <param name="testMode">Test mode, or <see langword="null"/> without it.</param>
    /// <param name="labels">The texts of the Tab view.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    public DockNoticeWindow(
        PanelViewModel panel,
        TestModeViewModel? testMode,
        DockLabels labels,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch
    )
        : base(
            new SurfaceId(SurfaceKind.Notice, Instance),
            registry,
            time,
            theme,
            touch,
            static (_, _, _) => { },
            SurfaceLook.SideWindow
        )
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(labels);
        _notices = panel.Notices;
        _labels = labels;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        _text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _undo.SetResourceReference(FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _cancel.SetResourceReference(FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _undo.Click += (_, _) => _notices.Undo();
        _cancel.Click += (_, _) => _notices.Cancel();
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_icon, Dock.Left);
        DockPanel.SetDock(_undo, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        row.Children.Add(_icon);
        row.Children.Add(_undo);
        row.Children.Add(_cancel);
        row.Children.Add(_text);
        _notice = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(Radii.Button),
            Padding = new Thickness(10, 4, 4, 4),
            Margin = new Thickness(8),
            MinHeight = Clicalo.Domain.Catalog.PanelSizes.Layout.PanelNoticeBarHeightPx,
        };
        _announcer = new LiveAnnouncer(_text);

        _admin = new AdminNoticeView(panel.Admin) { Margin = new Thickness(8, 8, 8, 0) };
        var stack = new StackPanel();
        if (testMode is not null)
        {
            _testMode = new TestModeIndicator(testMode) { Margin = new Thickness(8, 8, 8, 0) };
            _ = stack.Children.Add(_testMode);
        }

        _ = stack.Children.Add(_admin);
        _ = stack.Children.Add(_notice);
        Width = SurfaceWidth;
        SizeToContent = SizeToContent.Height;
        Content = stack;

        _notices.PropertyChanged += OnChanged;
        _labels.PropertyChanged += OnChanged;
        IsVisibleChanged += (_, _) => Say();
        Refresh();
    }

    /// <summary>The text of the notice (the live region).</summary>
    public TextBlock MessageText => _text;

    /// <summary>[undo].</summary>
    public TouchButton UndoButton => _undo;

    /// <summary>[cancel].</summary>
    public TouchButton CancelButton => _cancel;

    /// <summary>The administrator notice.</summary>
    public AdminNoticeView AdminNotice => _admin;

    /// <summary>What it announced last, and how.</summary>
    public LiveAnnouncer Announcer => _announcer;

    /// <summary>Whether the row of the notice shows (a notice is on show).</summary>
    public bool ShowsNotice => _notice.Visibility == Visibility.Visible;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        foreach (var target in _admin.TapTargets)
        {
            yield return SurfaceTarget.Button(target.Element, target.Tap);
        }

        if (_notices.Tone == NoticeTone.Rest)
        {
            yield break;
        }

        if (_notices.CanUndo)
        {
            yield return SurfaceTarget.Button(_undo, _notices.Undo);
        }

        if (_notices.CanCancel)
        {
            yield return SurfaceTarget.Button(_cancel, _notices.Cancel);
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _notices.PropertyChanged -= OnChanged;
        _labels.PropertyChanged -= OnChanged;
        _admin.Detach();
        _testMode?.Detach();
        base.OnClosed(e);
    }

    private static TouchButton NoticeButton(ButtonAppearance appearance) =>
        new()
        {
            Appearance = appearance,
            Height = ButtonHeight,
            Padding = new Thickness(10, 0, 10, 0),
            Focusable = false,
            IsTabStop = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Title = _labels.Notices;
        var tone = _notices.Tone;

        // At rest the Tab view shows no «Listo»: the surface is there only while there is something to tell.
        _notice.Visibility = SurfaceParts.Shown(tone != NoticeTone.Rest);
        _icon.Symbol = _notices.Icon;
        var warning = tone == NoticeTone.Warning;
        _notice.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(warning ? ColorToken.WarnWash : ColorToken.Card)
        );
        _icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(warning ? ColorToken.Warn : ColorToken.Accent)
        );
        _text.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        _undo.Content = _notices.UndoName;
        _undo.Visibility = SurfaceParts.Shown(_notices.CanUndo);
        _cancel.Content = _notices.CancelName;
        _cancel.Visibility = SurfaceParts.Shown(_notices.CanCancel);
        Say();
        RefreshTargets();
    }

    /// <summary>
    /// Shows the notice and announces it once as a live region (AVI-001): polite, or assertive for a warning. A surface
    /// that is not on screen announces nothing; it does when it appears.
    /// </summary>
    private void Say()
    {
        var tone = _notices.Tone;
        var message = tone == NoticeTone.Rest ? string.Empty : _notices.Message;
        if (!IsVisible || message.Length == 0)
        {
            _text.Text = message;
            _announced = string.Empty;
            return;
        }

        if (string.Equals(message, _announced, StringComparison.Ordinal))
        {
            return;
        }

        _announced = message;
        _announcer.Announce(
            message,
            tone == NoticeTone.Warning ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite
        );
    }
}
