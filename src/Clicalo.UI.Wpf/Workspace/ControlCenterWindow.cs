using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Presentation.ControlCenter;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.About;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.Internal;
using Clicalo.UI.Wpf.Workspace.SystemSection;
using Clicalo.UI.Wpf.Workspace.Templates;
using Clicalo.UI.Wpf.Workspace.TouchPrecision;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The Control Center (docs/05, CCM-001 to CCM-005): a normal window that can be activated, moved and resized (1120 ×
/// 680 by default, at least 760 × 520 or the work area when it is smaller), with the title bar of 52 (logo, «Clícalo ›
/// Centro de control › sección», ES/EN and ✕ of 44 × 40), the side menu of 220 (72 with icons only below 1240), the
/// section and the status bar of 48. Esc leaves a field first, then closes a menu, then the window.
/// </summary>
/// <remarks>
/// It never activates itself (<c>Window.Activate</c> is banned): it is shown without activation and the composition
/// root brings it to the front through the <c>ControlCenter</c> foreground lease (blueprint §3.6, CCM-004). Closing it
/// hides it: <see cref="CloseRequested"/> lets the composition root give the foreground back.
/// </remarks>
public sealed class ControlCenterWindow : Window
{
    /// <summary>The default width (CCM-001).</summary>
    public const double DefaultWidth = ControlCenterPlacer.DefaultWidth;

    /// <summary>The default height (CCM-001).</summary>
    public const double DefaultHeight = ControlCenterPlacer.DefaultHeight;

    private const double LeastWidth = ControlCenterPlacer.LeastWidth;
    private const double LeastHeight = ControlCenterPlacer.LeastHeight;
    private const double NarrowBelow = 1240;
    private const double TitleHeight = 52;
    private const double StatusHeight = 48;
    private const double WideNav = 220;
    private const double NarrowNav = 72;

    private readonly ControlCenterViewModel _viewModel;
    private readonly Border _root = new() { Focusable = true, FocusVisualStyle = null };
    private readonly ContentControl _crumb = new() { Focusable = false };
    private readonly ContentControl _languages = new() { Focusable = false };
    private readonly ContentControl _nav = new() { Focusable = false };
    private readonly ContentControl _section = new() { Focusable = false };
    private readonly ContentControl _status = new() { Focusable = false };
    private readonly ColumnDefinition _navColumn = new() { Width = new GridLength(WideNav) };
    private readonly ShortcutsSectionView _shortcuts;
    private readonly SystemSectionView? _system;
    private readonly TemplatesSectionView? _templates;
    private readonly GeneralSectionView _general;
    private readonly TouchPrecisionView _touch;
    private readonly AboutSectionView? _about;
    private readonly CcButton _close;
    private readonly TextBlock _statusText = Ui.Text(string.Empty, 13);
    private readonly LiveAnnouncer _statusAnnouncer;
    private string? _announced;
    private bool _narrow;
    private bool _closing;

    /// <summary>Creates the window of <paramref name="viewModel"/>, painted by <paramref name="theme"/>.</summary>
    /// <param name="viewModel">The frame.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    public ControlCenterWindow(ControlCenterViewModel viewModel, ThemeService theme)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(theme);
        _viewModel = viewModel;
        _statusAnnouncer = new LiveAnnouncer(_statusText);
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
        WindowChrome.SetWindowChrome(
            this,
            new WindowChrome
            {
                CaptionHeight = TitleHeight,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false,
            }
        );
        Ui.Ink(this, BackgroundProperty, ColorToken.Win);
        Ui.Ink(_root, Border.BackgroundProperty, ColorToken.Win);
        _shortcuts = new ShortcutsSectionView(viewModel.Shortcuts);
        _system = viewModel.System is { } system ? new SystemSectionView(system) : null;
        _templates = viewModel.Templates is { } templates
            ? new TemplatesSectionView(templates)
            : null;
        _general = new GeneralSectionView(viewModel.General);
        _touch = new TouchPrecisionView(viewModel.TouchPrecision);
        _about = viewModel.About is { } about ? new AboutSectionView(about) : null;
        _close = Ui.Button(Ui.Icon("close", 24), string.Empty, viewModel.Close, height: 40);
        _close.Width = 44;
        _close.Padding = new Thickness(0);
        _close.BorderThickness = new Thickness(0);
        _close.MouseEnter += (_, _) =>
            CcChrome.Paint(_close, ColorToken.Danger, ColorToken.OnDanger, null);
        _close.MouseLeave += (_, _) => CcChrome.Paint(_close, null, ColorToken.Text, null);
        WindowChrome.SetIsHitTestVisibleInChrome(_close, true);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleHeight) });
        layout.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
        );
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(StatusHeight) });
        layout.Children.Add(TitleBar());
        var body = new Grid();
        body.ColumnDefinitions.Add(_navColumn);
        body.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        var nav = new Border
        {
            Child = new TouchPanScrollViewer
            {
                Content = _nav,
                Padding = new Thickness(10, 12, 10, 12),
            },
            BorderThickness = new Thickness(0, 0, 1, 0),
        };
        Ui.Ink(nav, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(nav, Border.BorderBrushProperty, ColorToken.Border);
        body.Children.Add(nav);
        Grid.SetColumn(_section, 1);
        body.Children.Add(_section);
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        var status = new Border
        {
            Child = _status,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 0, 8, 0),
        };
        Ui.Ink(status, Border.BorderBrushProperty, ColorToken.Border);
        Ui.Ink(status, Border.BackgroundProperty, ColorToken.Side);
        Grid.SetRow(status, 2);
        layout.Children.Add(status);
        _root.Child = layout;
        Content = _root;

        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) => ApplyWidth();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        // ACC-004: a section that draws itself again does not take the keyboard away.
        _ = FocusKeeper.Attach(this);
        Render();
    }

    /// <summary>The person asked to close it (✕, Esc, Alt+F4): the composition root hides it and restores the foreground.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The window handle, for the foreground lease.</summary>
    public WindowToken Token => new(new WindowInteropHelper(this).EnsureHandle());

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

    private double CurrentScale()
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return scale > 0 ? scale : 1;
    }

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

    private void ApplyLeastSize(Rect? workArea = null)
    {
        var area = workArea ?? SystemParameters.WorkArea;
        MinWidth = Math.Min(LeastWidth, area.Width);
        MinHeight = Math.Min(LeastHeight, area.Height);
    }

    private void ApplyWidth()
    {
        var narrow = ActualWidth < NarrowBelow;
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;
        _navColumn.Width = new GridLength(narrow ? NarrowNav : WideNav);
        _shortcuts.SetNarrow(narrow);
        _templates?.SetNarrow(narrow);
        Render();
    }

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

        _viewModel.Escape();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private Border TitleBar()
    {
        var bar = new DockPanel { LastChildFill = true };
        var back = new Border
        {
            Child = bar,
            Padding = new Thickness(16, 0, 8, 0),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Ui.Ink(back, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(back, Border.BorderBrushProperty, ColorToken.Border);
        DockPanel.SetDock(_close, Dock.Right);
        bar.Children.Add(_close);
        DockPanel.SetDock(_languages, Dock.Right);
        _languages.Margin = new Thickness(0, 0, 8, 0);
        _languages.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(_languages);
        var logo = Logo();
        DockPanel.SetDock(logo, Dock.Left);
        bar.Children.Add(logo);
        _crumb.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(_crumb);
        return back;
    }

    private StackPanel Logo()
    {
        var mark = new Canvas { Width = 30, Height = 30 };
        var square = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
        };
        Ui.Ink(square, Border.BackgroundProperty, ColorToken.Accent);
        mark.Children.Add(square);
        mark.Children.Add(Stroke(13, 13, 5, 12, 0, 1));
        mark.Children.Add(Stroke(14, 4, 4, 7, 32, 1));
        mark.Children.Add(Stroke(21, 5, 2, 4, 72, 0.75));
        var word = Ui.Text(_viewModel.AppName, 16, bold: true);
        word.Margin = new Thickness(10, 0, 0, 0);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(mark);
        row.Children.Add(word);
        return row;
    }

    private static Rectangle Stroke(
        double left,
        double top,
        double width,
        double height,
        double angle,
        double opacity
    )
    {
        var stroke = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = width / 2,
            RadiusY = width / 2,
            Opacity = opacity,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(angle),
        };
        Ui.Ink(stroke, Shape.FillProperty, ColorToken.OnAccent);
        Canvas.SetLeft(stroke, left);
        Canvas.SetTop(stroke, top);
        return stroke;
    }

    private void Render()
    {
        Title = _viewModel.Title;
        AutomationProperties.SetName(_close, _viewModel.CloseName);
        _close.ToolTip = _viewModel.CloseHelp;
        var crumb = Ui.Row(
            6,
            Ui.Icon("chevron_right", 18, ColorToken.Muted),
            Ui.Text(_viewModel.Title, 14),
            _narrow ? null : Ui.Icon("chevron_right", 18, ColorToken.Muted),
            _narrow ? null : Ui.Text(_viewModel.SectionTitle, 14, ink: ColorToken.Muted)
        );
        crumb.Margin = new Thickness(10, 0, 0, 0);
        _crumb.Content = crumb;
        var languages = Ui.Row(2);
        foreach (var option in _viewModel.Languages)
        {
            var button = Ui.Choice(
                Ui.Text(
                    option.Label,
                    12,
                    bold: true,
                    ink: option.Selected ? ColorToken.OnAccent : ColorToken.Text
                ),
                option.Name,
                option.Selected,
                () => _viewModel.SetLanguage(option.Code),
                44,
                7,
                offFill: null
            );
            if (option.Selected)
            {
                CcChrome.Paint(button, ColorToken.Accent, ColorToken.OnAccent, null);
            }
            else
            {
                CcChrome.Paint(button, null, ColorToken.Text, null);
            }

            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(10, 0, 10, 0);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            languages.Children.Add(button);
        }

        var group = Ui.Card(languages, ColorToken.Card, null, 10, new Thickness(3));
        AutomationProperties.SetName(group, _viewModel.LanguageName);
        _languages.Content = group;
        _nav.Content = Nav();
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
        _status.Content = Status();
    }

    private StackPanel Nav()
    {
        var column = Ui.Column(4);
        foreach (var item in _viewModel.Nav)
        {
            if (item.SeparatorBefore)
            {
                column.Children.Add(Ui.Line(8));
            }

            var icon = Ui.Icon(item.Icon, 22, item.Selected ? ColorToken.Accent : ColorToken.Text);
            UIElement content;
            if (_narrow)
            {
                var layers = new Grid();
                layers.Children.Add(icon);
                if (item.Count > 0)
                {
                    layers.Children.Add(
                        Badge(item.Count, HorizontalAlignment.Right, VerticalAlignment.Top)
                    );
                }

                content = layers;
            }
            else
            {
                var row = new DockPanel { LastChildFill = true };
                icon.Margin = new Thickness(0, 0, 10, 0);
                DockPanel.SetDock(icon, Dock.Left);
                row.Children.Add(icon);
                if (item.Count > 0)
                {
                    var badge = Badge(
                        item.Count,
                        HorizontalAlignment.Right,
                        VerticalAlignment.Center
                    );
                    DockPanel.SetDock(badge, Dock.Right);
                    row.Children.Add(badge);
                }

                row.Children.Add(
                    Ui.Text(
                        item.Label,
                        15,
                        bold: true,
                        ink: item.Selected ? ColorToken.Accent : ColorToken.Text,
                        wrap: true
                    )
                );
                content = row;
            }

            var button = Ui.Choice(
                content,
                item.Label,
                item.Selected,
                () => _viewModel.Select(item.Section),
                46,
                10,
                offFill: null
            );
            if (item.Selected)
            {
                CcChrome.Paint(button, ColorToken.AccentWash, ColorToken.Accent, null);
            }
            else
            {
                CcChrome.Paint(button, null, ColorToken.Text, null);
            }

            button.BorderThickness = new Thickness(0);
            button.Height = double.NaN;
            button.MinHeight = 46;
            button.Padding = _narrow ? new Thickness(0) : new Thickness(12, 4, 8, 4);
            button.HorizontalContentAlignment = _narrow
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
            AutomationProperties.SetItemStatus(button, item.CountName);
            column.Children.Add(button);
        }

        return column;
    }

    private static Border Badge(
        int count,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical
    )
    {
        var badge = Ui.Card(
            Ui.Text(
                count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                12,
                bold: true,
                ink: ColorToken.OnWarn
            ),
            ColorToken.Warn,
            null,
            11,
            new Thickness(6, 1, 6, 1)
        );
        badge.MinWidth = 22;
        badge.HorizontalAlignment = horizontal;
        badge.VerticalAlignment = vertical;
        badge.IsHitTestVisible = false;
        if (badge.Child is FrameworkElement text)
        {
            text.HorizontalAlignment = HorizontalAlignment.Center;
        }

        return badge;
    }

    private StackPanel Soon()
    {
        var column = Ui.Column(
            8,
            Ui.Text(_viewModel.SectionTitle, 24, bold: true),
            Ui.Text(_viewModel.Soon, 14, ink: ColorToken.Muted, wrap: true)
        );
        column.Margin = new Thickness(24);
        return column;
    }

    private DockPanel Status()
    {
        var status = _viewModel.Status;
        var row = new DockPanel
        {
            LastChildFill = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (status.CanUndo)
        {
            var undo = Ui.Button(
                Ui.IconLabel("undo", status.UndoText, 18, 13),
                status.UndoName,
                _viewModel.Undo,
                ColorToken.Card,
                stroke: ColorToken.Border
            );
            DockPanel.SetDock(undo, Dock.Right);
            row.Children.Add(undo);
        }

        var icon = Ui.Icon(status.Icon, 18, status.IsWarning ? ColorToken.Warn : ColorToken.Muted);
        icon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        // ACC-001: the status bar is a live region. Its text block stays, so a new message is a change of that
        // region and is announced: polite for a notice, assertive for a warning. Going back to [saved] is not read.
        if (_statusText.Parent is Panel old)
        {
            old.Children.Remove(_statusText);
        }

        Ui.Ink(
            _statusText,
            TextBlock.ForegroundProperty,
            status.IsWarning ? ColorToken.Text : ColorToken.Muted
        );
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
            _statusText.Text = status.Text;
            if (!status.IsNotice)
            {
                _announced = null;
            }
        }

        row.Children.Add(_statusText);
        return row;
    }
}
