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
/// <para>
/// It projects the same view models as the panel (<see cref="NoticeBarViewModel"/>, <see cref="AdminNoticeViewModel"/>,
/// <see cref="TestModeViewModel"/>), so the Tab view can never tell something else than the panel would.
/// </para>
/// <para>
/// D21: the Compact view has no status row and shows its notices here for a moment, under the panel, on a face that
/// lets the app behind show through (<see cref="ServesCompact"/>), without ↻ Repeat. The Tab view keeps a plain
/// confirmation («Ctrl+S enviado a Word») off the screen and only says it (<see cref="QuietAnnouncer"/>).
/// </para>
/// </remarks>
public sealed class DockNoticeWindow : TouchSurface
{
    /// <summary>The instance of the notice surface in the registry; the floating «Release all» is the first.</summary>
    public const int Instance = 2;

    private const double SurfaceWidth = 300;

    private readonly NoticeBarViewModel _notices;
    private readonly DockLabels _labels;
    private readonly AdminNoticeView _admin;
    private readonly TestModeIndicator? _testMode;
    private readonly Border _notice;
    private readonly SymbolIcon _icon = new()
    {
        Size = IconBox.Small,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // The window grows with its text: the part of a notice that says why or what to do is never cut (D21 review).
    private readonly StyledText _text = new(string.Empty, TextStyleToken.BodySmall)
    {
        MaxLines = 4,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(Space.S2, 0, Space.S2, 0),
    };
    private readonly TouchButton _undo = NoticeButton(ButtonVariant.Tertiary, "undo");
    private readonly TouchButton _cancel = NoticeButton(ButtonVariant.Tertiary, null);
    private readonly LiveAnnouncer _announcer;
    private readonly Border _face = new();
    private readonly StackPanel _tabParts = new();
    private string _announced = string.Empty;
    private string _said = string.Empty;
    private bool _translucent;

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
        _face.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        ApplyFace();
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

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
            CornerRadius = new CornerRadius(Radii.Medium),
            Padding = new Thickness(Space.S2, 0, 0, 0),
            Margin = new Thickness(Space.S2),
            MinHeight = ControlHeight.Small,
        };
        _announcer = new LiveAnnouncer(_text);

        _admin = new AdminNoticeView(panel.Admin)
        {
            Margin = new Thickness(Space.S2, Space.S2, Space.S2, 0),
        };
        if (testMode is not null)
        {
            _testMode = new TestModeIndicator(testMode)
            {
                Margin = new Thickness(Space.S2, Space.S2, Space.S2, 0),
            };
            _ = _tabParts.Children.Add(_testMode);
        }

        _ = _tabParts.Children.Add(_admin);
        var stack = new StackPanel();
        _ = stack.Children.Add(_tabParts);
        _ = stack.Children.Add(_notice);
        Width = SurfaceWidth;
        FixedWidth = SurfaceWidth;
        SizeToContent = SizeToContent.Height;
        var root = new Grid();
        root.Children.Add(_face);
        root.Children.Add(stack);
        Content = root;

        _notices.PropertyChanged += OnChanged;
        _labels.PropertyChanged += OnChanged;
        IsVisibleChanged += (_, _) => Say();
        Theme.Changed += OnThemeChanged;
        Refresh();
    }

    /// <summary>
    /// Whether the surface serves the Compact view (D21): only the moment of its notice, on a face that lets what is
    /// behind show through (never in high contrast, where the text keeps its full contrast); the administrator notice and
    /// test mode are inside the panel then.
    /// </summary>
    public bool ServesCompact
    {
        get => _translucent;
        set
        {
            _translucent = value;
            _tabParts.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            ApplyFace();
        }
    }

    /// <summary>
    /// Takes <paramref name="width"/> (the width of the Compact panel it shows under, so their edges line up), or its own
    /// width beside the bar or the bubble when <see langword="null"/>.
    /// </summary>
    /// <param name="width">The width in DIPs, or <see langword="null"/>.</param>
    public void UseWidth(double? width)
    {
        var wanted = width ?? SurfaceWidth;
        if (Math.Abs(Width - wanted) > 0.5)
        {
            Width = wanted;
            FixedWidth = wanted;
        }
    }

    /// <summary>
    /// Where a notice is said while the surface is not on screen (the Tab view keeps a plain confirmation off the
    /// screen): the announcer of the bar or the handle on show, or <see langword="null"/> when the panel says it itself.
    /// </summary>
    public LiveAnnouncer? QuietAnnouncer { get; set; }

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

    /// <summary>Whether touches go through the surface to the app (the moment of a confirmation in Compact, D21).</summary>
    public bool LetsTouchesThrough => IsClickThrough;

    /// <summary>Whether the row of the notice shows (a notice is on show).</summary>
    public bool ShowsNotice => _notice.Visibility == Visibility.Visible;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        if (!_translucent)
        {
            foreach (var target in _admin.TapTargets)
            {
                yield return SurfaceTarget.Button(target.Element, target.Tap);
            }
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
        Theme.Changed -= OnThemeChanged;
        _admin.Detach();
        _testMode?.Detach();
        base.OnClosed(e);
    }

    private static TouchButton NoticeButton(ButtonVariant variant, string? symbol) =>
        new()
        {
            Variant = variant,
            Symbol = symbol,
            Focusable = false,
            IsTabStop = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S1, 0, 0, 0),
        };

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyFace();

    /// <summary>
    /// The window itself stays all but transparent (alpha 1, so a touch still lands on it) and the face under the content
    /// gives it its surface: solid, or translucent for the moment of a notice of the Compact view.
    /// </summary>
    private void ApplyFace()
    {
        // The moment of a confirmation under a Compact panel lets the app show through and takes no touch: a tap there
        // reaches the app, so the moment never stands in the way (D21 review: it swallowed taps for 3 to 10 s). A
        // warning, [undo] or [cancel] keeps a solid face and its buttons.
        var moment =
            _translucent
            && _notices.Tone != NoticeTone.Warning
            && !_notices.CanUndo
            && !_notices.CanCancel;
        Background = SurfaceParts.TouchBand;
        var highContrast = Theme.Effective is ThemeId.HighContrast or ThemeId.SystemHighContrast;
        _face.Opacity = moment && !highContrast ? 0.8 : 1;
        IsClickThrough = moment;
    }

    private void Refresh()
    {
        Title = _labels.Notices;
        var tone = _notices.Tone;
        ApplyFace();

        // At rest the Tab view shows no «Listo»: the surface is there only while there is something to tell.
        _notice.Visibility = SurfaceParts.Shown(tone != NoticeTone.Rest);
        _icon.Symbol = _notices.Icon;
        var warning = tone == NoticeTone.Warning;
        // The notice lies on the surface itself, as the status bar of the panel does; a warning takes the wash of warn
        // (D12: warn is attention), never a box of its own with a border.
        if (warning)
        {
            _notice.SetResourceReference(
                Border.BackgroundProperty,
                ThemeBrushKey.For(ColorToken.WarnWash)
            );
        }
        else
        {
            _notice.Background = System.Windows.Media.Brushes.Transparent;
        }

        _icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(warning ? ColorToken.WarnText : ColorToken.Muted)
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
        var urgency =
            tone == NoticeTone.Warning ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite;
        if (!IsVisible || message.Length == 0)
        {
            _text.Text = message;
            _announced = string.Empty;

            // A confirmation kept off the screen is still said once by the surface on show (ACC-001); any other notice
            // is said by this window when it appears, only once (D21 review: it was said twice).
            if (
                message.Length > 0
                && _notices.IsConfirmation
                && QuietAnnouncer is { } quiet
                && !string.Equals(message, _said, StringComparison.Ordinal)
            )
            {
                quiet.Say(message, urgency);
            }

            _said = message;
            return;
        }

        _said = message;

        if (string.Equals(message, _announced, StringComparison.Ordinal))
        {
            return;
        }

        _announced = message;
        _announcer.Announce(message, urgency);
    }
}
