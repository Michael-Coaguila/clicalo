using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Presentation.ControlCenter;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.UI.Wpf.Workspace.About;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;
using Clicalo.UI.Wpf.Workspace.SystemSection;
using Clicalo.UI.Wpf.Workspace.Templates;
using Clicalo.UI.Wpf.Workspace.TouchPrecision;
using Clicalo.UI.Wpf.Workspace.Welcome;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The Control Center (docs/05, CCM-001 to CCM-005) in direction C (D12, D13): a real Windows window (1120 × 680 by
/// default, at least 760 × 520 or the work area when it is smaller) with the title bar of
/// <see cref="ComponentSize.TitleBar"/> (logo, «Clícalo › Centro de control › sección» at every width, ES/EN and the
/// three Windows buttons), the rail of <see cref="RailWidth"/> with its labels always shown and the counter beside
/// each icon, the section, and the status bar of <see cref="ControlHeight.Small"/> (✓ and the message, Deshacer and the
/// resize grip). Esc leaves a field first, then closes a menu, then the window.
/// </summary>
/// <remarks>
/// It never activates itself (<c>Window.Activate</c> is banned): it is shown without activation and the composition
/// root brings it to the front through the <c>ControlCenter</c> foreground lease (blueprint §3.6, CCM-004). Closing it
/// hides it: <see cref="CloseRequested"/> lets the composition root give the foreground back. Each section lays itself
/// out for the width it gets (<see cref="ApplyWidth(double)"/>): the narrow window and the maximized one are designed
/// in the sections, never squeezed.
/// </remarks>
public sealed class ControlCenterWindow : Window
{
    /// <summary>The default width (CCM-001).</summary>
    public const double DefaultWidth = ControlCenterPlacer.DefaultWidth;

    /// <summary>The default height (CCM-001).</summary>
    public const double DefaultHeight = ControlCenterPlacer.DefaultHeight;

    /// <summary>The width of the rail: two touch targets (88 in the design of C).</summary>
    public const double RailWidth = ControlHeight.Small * 2;

    private const double LeastWidth = ControlCenterPlacer.LeastWidth;
    private const double LeastHeight = ControlCenterPlacer.LeastHeight;

    private readonly ControlCenterViewModel _viewModel;
    private readonly Border _root = new() { Focusable = true, FocusVisualStyle = null };
    private readonly ContentControl _crumb = new() { Focusable = false };
    private readonly ContentControl _languages = new() { Focusable = false };
    private readonly StackPanel _nav = new();
    private readonly ThemedScrollViewer _railScroll = new();
    private readonly ContentControl _section = new() { Focusable = false };
    private readonly StatusBar _status = new();
    private readonly ShortcutsSectionView _shortcuts;
    private readonly SystemSectionView? _system;
    private readonly TemplatesSectionView? _templates;
    private readonly GeneralSectionView _general;
    private readonly TouchPrecisionView _touch;
    private readonly AboutSectionView? _about;
    private readonly TitleBar _titleBar = new();
    private readonly Grid _grip = Grip();
    private readonly LiveAnnouncer _statusAnnouncer;
    private readonly LiveAnnouncer _announcer;
    private string? _announced;
    private bool _closing;

    /// <summary>Creates the window of <paramref name="viewModel"/>, painted by <paramref name="theme"/>.</summary>
    /// <param name="viewModel">The frame.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    public ControlCenterWindow(ControlCenterViewModel viewModel, ThemeService theme)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(theme);
        _viewModel = viewModel;
        _statusAnnouncer = new LiveAnnouncer(_status.Message);
        // ACC-001: the single way of the window to say the lines its sections draw again on every change.
        _announcer = new LiveAnnouncer(this);
        theme.Attach(this);
        Width = DefaultWidth;
        Height = DefaultHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = false;
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        UseLayoutRounding = true;
        ApplyLeastSize();
        Ui.Ink(this, BackgroundProperty, ColorToken.Win);
        Ui.Ink(_root, Border.BackgroundProperty, ColorToken.Win);
        _shortcuts = new ShortcutsSectionView(
            viewModel.Shortcuts,
            (text, urgency) => SayFrom(_shortcuts, text, urgency)
        );
        _system = viewModel.System is { } system
            ? new SystemSectionView(system, (text, urgency) => SayFrom(_system, text, urgency))
            : null;
        _templates = viewModel.Templates is { } templates
            ? new TemplatesSectionView(templates)
            : null;
        _general = new GeneralSectionView(
            viewModel.General,
            (text, urgency) => SayFrom(_general, text, urgency)
        );
        _touch = new TouchPrecisionView(viewModel.TouchPrecision);
        _about = viewModel.About is { } about ? new AboutSectionView(about) : null;
        // D13: a real Windows window, with minimize, maximize (and Snap) and close, and edges that resize it.
        _titleBar.Leading = TitleLeading();
        _titleBar.Trailing = _languages;
        _titleBar.ResizeGrip = _grip;
        _titleBar.Attach(this, viewModel.Close);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
        );
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(_titleBar);
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RailWidth) });
        body.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        // The rail scrolls when the window is too short for its six sections; the channel of its bar is its right
        // margin, so nothing moves when the bar appears, and the items keep Space.S1 from it and from the edge, so
        // the ring of «elegido» never touches the bar.
        _railScroll.Content = _nav;
        _railScroll.Padding = new Thickness(Space.S1, Space.S3, Space.S1, Space.S3);
        _railScroll.SizeChanged += (_, _) => RevealSelected();
        var rail = new Border
        {
            Child = _railScroll,
            BorderThickness = new Thickness(0, 0, Theming.Generated.Stroke.Line, 0),
        };
        Ui.Ink(rail, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(rail, Border.BorderBrushProperty, ColorToken.Border);
        body.Children.Add(rail);
        Grid.SetColumn(_section, 1);
        body.Children.Add(_section);
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        Grid.SetRow(_status, 2);
        layout.Children.Add(_status);
        _root.Child = layout;
        Content = _root;

        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) => ApplyWidth();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        // EDI-010: the recording only lives while the window has the keyboard. Once another window takes it, the
        // keys that were held are released out of sight, and a stale modifier would slip into the next combination.
        Deactivated += (_, _) => viewModel.Shortcuts.Editor.StopRecording();
        // ACC-004: a section that draws itself again does not take the keyboard away.
        _ = FocusKeeper.Attach(this);
        Render();
    }

    /// <summary>The person asked to close it (✕, Esc, Alt+F4): the composition root hides it and restores the foreground.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The window handle, for the foreground lease.</summary>
    public WindowToken Token => new(new WindowInteropHelper(this).EnsureHandle());

    /// <summary>
    /// What the window said last of the lines its sections draw again on every change (ACC-001): «Probar» and its
    /// question, a button armed by its first tap, the question of a binding, the state of the updates.
    /// </summary>
    public LiveAnnouncer Announcer => _announcer;

    /// <summary>Closes it for good (the app is exiting).</summary>
    public void Destroy()
    {
        _closing = true;
        _viewModel.PropertyChanged -= OnChanged;
        _shortcuts.Detach();
        _system?.Detach();
        _general.Detach();
        _touch.Detach();
        _about?.Detach();
        Close();
    }

    /// <summary>
    /// Places it where <see cref="ControlCenterPlacer"/> planned (CCM-001, CCM-004): on that monitor, with that size,
    /// beside the panel. The plan is in physical pixels; WPF converts <see cref="Window.Left"/> and
    /// <see cref="Window.Top"/> with the scale of the monitor the window is on now, so it first moves onto the monitor
    /// and then takes its size and its place with that monitor's scale.
    /// </summary>
    /// <param name="spot">Where it opens.</param>
    public void Place(ControlCenterSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        _ = new WindowInteropHelper(this).EnsureHandle();
        var scale = spot.Monitor.Scale > 0 ? spot.Monitor.Scale : 1;
        var work = spot.Monitor.WorkArea;
        ApplyLeastSize(new Rect(0, 0, work.Width / scale, work.Height / scale));
        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        var before = CurrentScale();
        Left = spot.Bounds.Left / before;
        Top = spot.Bounds.Top / before;
        Width = Math.Max(MinWidth, spot.Bounds.Width / scale);
        Height = Math.Max(MinHeight, spot.Bounds.Height / scale);
        var after = CurrentScale();
        Left = spot.Bounds.Left / after;
        Top = spot.Bounds.Top / after;
    }

    /// <summary>
    /// Where the window is now, to remember it (CCM-001, D9): its restored rectangle in physical pixels and whether it
    /// is maximized.
    /// </summary>
    public (PhysicalRect Bounds, bool Maximized) ReadPlacement()
    {
        var scale = CurrentScale();
        var rect =
            WindowState == WindowState.Normal || RestoreBounds.IsEmpty
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;
        return (
            new PhysicalRect(
                (int)Math.Round(rect.Left * scale),
                (int)Math.Round(rect.Top * scale),
                (int)Math.Round(rect.Width * scale),
                (int)Math.Round(rect.Height * scale)
            ),
            WindowState == WindowState.Maximized
        );
    }

    /// <summary>
    /// Maximizes it, as it was left (D9). Only once it is in front through its lease: maximizing a window activates
    /// it, and the Control Center never activates itself.
    /// </summary>
    public void Maximize() => WindowState = WindowState.Maximized;

    /// <summary>
    /// Lays the sections out for a window of <paramref name="width"/> (CCM-005): each section gets the width beside the
    /// rail and chooses its own designed layout (in «Atajos», three columns, or the profiles in a selector and the
    /// editor as a page when they do not fit). The window calls it whenever its size changes; a preview or an audit of
    /// a window that is never shown calls it with the width it lays the content out at.
    /// </summary>
    /// <param name="width">The width of the window, in device-independent pixels.</param>
    public void ApplyWidth(double width)
    {
        var content = Math.Max(0, width - RailWidth);
        _shortcuts.ApplyWidth(content);
        _templates?.ApplyWidth(content);
    }

    /// <summary>
    /// In a window too narrow for the three columns of «Atajos», shows the editor of the shortcut in view as its own
    /// page, as a tap on its tile does (the previews and the audits of the narrow window).
    /// </summary>
    public void ShowShortcutPage() => _shortcuts.ShowPage();

    /// <summary>
    /// In a window too narrow for the three columns of «Atajos», opens the list of profiles of its selector, as a tap on
    /// the selector does (the previews and the audits of the narrow window).
    /// </summary>
    public void ShowShortcutProfiles() => _shortcuts.OpenProfiles();

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_closing)
        {
            e.Cancel = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    private double CurrentScale()
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return scale > 0 ? scale : 1;
    }

    private void ApplyLeastSize(Rect? workArea = null)
    {
        var area = workArea ?? SystemParameters.WorkArea;
        MinWidth = Math.Min(LeastWidth, area.Width);
        MinHeight = Math.Min(LeastHeight, area.Height);
    }

    private void ApplyWidth() => ApplyWidth(ActualWidth);

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var editor = _viewModel.Shortcuts.Editor;
        if (editor.IsRecording)
        {
            e.Handled = true;
            editor.RecordKeyUp(RecordedKeys.IdOf(e));
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var editor = _viewModel.Shortcuts.Editor;
        if (editor.IsRecording)
        {
            // EDI-010: while «Grabar con teclado» is on, every key the window receives belongs to the recording.
            e.Handled = true;
            editor.RecordKeyDown(RecordedKeys.IdOf(e));
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (Keyboard.FocusedElement is TextBox)
        {
            // CCM-001: Esc in a field only leaves the field.
            _ = Keyboard.Focus(_root);
            return;
        }

        // In «Atajos» of the narrow window, the list of profiles closes first, then the menus of the view model, then
        // the page of the editor; only then the window.
        if (_viewModel.Section == ControlCenterSection.Shortcuts)
        {
            if (
                _shortcuts.CloseProfiles()
                || _viewModel.Shortcuts.CloseMenu()
                || _shortcuts.ClosePage()
            )
            {
                return;
            }
        }

        _viewModel.Escape();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private StackPanel TitleLeading()
    {
        var logo = BrandMark.Mark(IconBox.Large);
        logo.Margin = new Thickness(0, 0, Space.S3, 0);
        _crumb.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(logo);
        row.Children.Add(_crumb);
        return row;
    }

    private void Render()
    {
        Title = _viewModel.Title;
        _titleBar.SetNames(
            _viewModel.MinimizeName,
            _viewModel.MaximizeName,
            _viewModel.RestoreName,
            _viewModel.CloseName
        );
        _titleBar.Close.ToolTip = _viewModel.CloseHelp;
        _crumb.Content = Crumb();
        _languages.Content = Languages();
        RenderNav();
        _section.Content = _viewModel.Section switch
        {
            ControlCenterSection.Shortcuts => _shortcuts,
            ControlCenterSection.System when _system is not null => _system,
            ControlCenterSection.Templates when _templates is not null => _templates,
            ControlCenterSection.Panel => _general,
            ControlCenterSection.Touch => _touch,
            ControlCenterSection.About when _about is not null => _about,
            _ => Soon(),
        };
        RenderStatus();
    }

    /// <summary>«Clícalo › Centro de control › sección», the same at every width; only the section ends in «…».</summary>
    private StackPanel Crumb()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (
            var part in new FrameworkElement[]
            {
                new StyledText(_viewModel.AppName, TextStyleToken.Heading),
                Chevron(),
                new StyledText(_viewModel.Title, TextStyleToken.Body, ColorToken.Muted),
                Chevron(),
                new StyledText(_viewModel.SectionTitle, TextStyleToken.Body)
                {
                    FontWeight = FontWeights.Bold,
                },
            }
        )
        {
            part.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(part);
        }

        return row;
    }

    private static SymbolIcon Chevron()
    {
        var chevron = Ui.Icon("chevron_right", IconBox.Small, ColorToken.Muted);
        chevron.Margin = new Thickness(Space.S1, 0, Space.S1, 0);
        return chevron;
    }

    /// <summary>ES | EN: two segments of 44 on a card of the bar, with air above and below (D12).</summary>
    private Border Languages()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var option in _viewModel.Languages)
        {
            var button = new OptionButton
            {
                Kind = OptionKind.Segment,
                Content = option.Label,
                IsSelected = option.Selected,
                MinWidth = ControlHeight.Small + Space.S3,
            };
            // Named as it reads («ES»), so voice access finds it by what is drawn (ACC-001); the language's own name
            // («Español») is its help, as S, M and L of the quick settings.
            AutomationProperties.SetName(button, option.Label);
            AutomationProperties.SetHelpText(button, option.Name);
            button.Click += (_, _) => _viewModel.SetLanguage(option.Code);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            row.Children.Add(button);
        }

        // The segments of 44 sit inside their card, as in a track, so the ring of the chosen one never merges with
        // its line, and the card of 48 keeps air above and below in the bar of 56.
        var group = Ui.Card(
            row,
            ColorToken.Card,
            ColorToken.Border,
            Radii.Medium,
            new Thickness(Space.S1 / 2)
        );
        AutomationProperties.SetName(group, _viewModel.LanguageName);
        return group;
    }

    /// <summary>
    /// The rail (CCM-002): every section with its icon and its label (two lines at most), the counter beside the icon,
    /// never over it, and the single look of «elegido» on the section in view.
    /// </summary>
    private void RenderNav()
    {
        _nav.Children.Clear();
        foreach (var item in _viewModel.Nav)
        {
            if (item.SeparatorBefore)
            {
                var line = new Border
                {
                    Height = Theming.Generated.Stroke.Line,
                    Margin = new Thickness(Space.S2, Space.S1, Space.S2, Space.S2),
                };
                Ui.Ink(line, Border.BackgroundProperty, ColorToken.Border);
                _nav.Children.Add(line);
            }

            var button = new OptionButton
            {
                Kind = OptionKind.Rail,
                Content = item.Label,
                Symbol = item.Icon,
                Count = item.Count,
                IsSelected = item.Selected,
                Margin = new Thickness(0, 0, 0, Space.S1),
            };
            AutomationProperties.SetName(button, item.Label);
            AutomationProperties.SetItemStatus(button, item.CountName);
            button.Click += (_, _) => _viewModel.Select(item.Section);
            _nav.Children.Add(button);
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RevealSelected);
    }

    /// <summary>
    /// Keeps the section in view visible in the rail when the window is too short for the six (CCM-005): in the
    /// smallest window «Clícalo y contacto» is under the fade otherwise.
    /// </summary>
    private void RevealSelected()
    {
        foreach (var child in _nav.Children)
        {
            if (child is OptionButton { IsSelected: true } selected && selected.IsLoaded)
            {
                selected.BringIntoView();
                return;
            }
        }
    }

    /// <summary>
    /// The resize grip of the corner (D13): the dots of the Windows grip, three, two and one towards the corner, in
    /// <c>muted</c>, in a target of 44 that resizes by dragging. An arrow read as a button that a tap would press.
    /// </summary>
    private static Grid Grip()
    {
        const int Rows = 3;
        var dots = new GeometryGroup();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = Rows - 1 - row; column < Rows; column++)
            {
                dots.Children.Add(
                    new RectangleGeometry(
                        new Rect(
                            column * Space.S1,
                            row * Space.S1,
                            Theming.Generated.Stroke.Selected,
                            Theming.Generated.Stroke.Selected
                        )
                    )
                );
            }
        }

        var path = new System.Windows.Shapes.Path
        {
            Data = dots,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(Space.S2),
        };
        Ui.Ink(path, System.Windows.Shapes.Shape.FillProperty, ColorToken.Muted);
        var grip = new Grid
        {
            Width = ControlHeight.Small,
            Height = ControlHeight.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grip.Children.Add(path);
        return grip;
    }

    private StackPanel Soon()
    {
        var column = Ui.Column(
            Space.S2,
            new StyledText(_viewModel.SectionTitle, TextStyleToken.Title),
            new StyledText(_viewModel.Soon, TextStyleToken.Body, ColorToken.Muted) { MaxLines = 0 }
        );
        column.Margin = new Thickness(Space.S6);
        return column;
    }

    /// <summary>
    /// The status bar (CCM-003): ✓ and the message at rest and after a change, ⚠ for a warning; Deshacer and the resize
    /// grip at its end. Its message is a live region (ACC-001).
    /// </summary>
    private void RenderStatus()
    {
        var status = _viewModel.Status;
        _status.Tone =
            status.IsWarning ? StatusTone.Warning
            : string.Equals(status.Icon, "block", StringComparison.Ordinal) ? StatusTone.Danger
            : StatusTone.Success;
        var end = new StackPanel { Orientation = Orientation.Horizontal };
        if (status.CanUndo)
        {
            var undo = new TouchButton
            {
                Content = status.UndoText,
                Symbol = "undo",
                Variant = ButtonVariant.Tertiary,
            };
            AutomationProperties.SetName(undo, status.UndoName);
            undo.Click += (_, _) => _viewModel.Undo();
            end.Children.Add(undo);
        }

        if (_grip.Parent is Panel old)
        {
            old.Children.Remove(_grip);
        }

        end.Children.Add(_grip);
        _status.Action = end;
        // ACC-001: the status bar is a live region. Its text block stays, so a new message is a change of that
        // region and is announced: polite for a notice, assertive for a warning. Going back to [saved] is not read.
        if (status.IsNotice && !string.Equals(_announced, status.Text, StringComparison.Ordinal))
        {
            _announced = status.Text;
            _statusAnnouncer.Announce(
                status.Text,
                status.IsWarning ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite
            );
        }
        else
        {
            _status.Text = status.Text;
            if (!status.IsNotice)
            {
                _announced = null;
            }
        }
    }

    /// <summary>
    /// Says <paramref name="text"/>, a line <paramref name="section"/> drew again, while that section is the one on show:
    /// a section out of view speaks no more than a live region out of the tree would (ACC-001).
    /// </summary>
    private void SayFrom(UIElement? section, string text, AnnouncementUrgency urgency)
    {
        if (section is not null && ReferenceEquals(_section.Content, section))
        {
            _announcer.Say(text, urgency);
        }
    }
}
