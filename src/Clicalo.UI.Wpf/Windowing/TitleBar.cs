using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing.Internal;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The title bar of a normal window of Clícalo (the Control Center, D13): <see cref="ComponentSize.TitleBar"/> high on
/// <c>side</c> with its line below; what the view puts in <see cref="Leading"/> (the logo and the path) and in
/// <see cref="Trailing"/> (the language), and the three Windows buttons in zones of
/// <see cref="ComponentSize.CaptionButton"/>: minimize, maximize or restore, and close (red under the pointer).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Attach"/> makes the window a real Windows window: the bar drags it and a double tap maximizes or restores
/// it (the caption of <see cref="WindowChrome"/>); the maximize zone answers <c>HTMAXBUTTON</c> to
/// <c>WM_NCHITTEST</c>, so Windows 11 shows the Snap layouts over it; the edges and
/// <see cref="ResizeGrip"/> resize it; and while it is maximized its content keeps out of the edges that Windows
/// puts beyond the monitor. It works at any scale and on any monitor: every hit test goes through
/// <see cref="System.Windows.Media.Visual.PointFromScreen"/>.
/// </para>
/// <para>
/// The window is never activated by the bar (<c>Window.Activate</c> is banned): its buttons send the same system
/// commands as the buttons of Windows. UI Automation sees three Buttons with Invoke, named with
/// <see cref="SetNames"/>, and the window itself keeps the WindowPattern of WPF.
/// </para>
/// </remarks>
public sealed class TitleBar : Border
{
    private readonly ContentControl _leading = new() { Focusable = false };
    private readonly ContentControl _trailing = new() { Focusable = false };
    private Window? _window;
    private Action? _close;
    private string _maximizeName = string.Empty;
    private string _restoreName = string.Empty;

    /// <summary>Creates the bar.</summary>
    public TitleBar()
    {
        Height = ComponentSize.TitleBar;
        BorderThickness = new Thickness(0, 0, 0, Stroke.Line);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Side));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        Minimize = new CaptionButton { Kind = CaptionKind.Minimize };
        Maximize = new CaptionButton { Kind = CaptionKind.Maximize };
        Close = new CaptionButton { Kind = CaptionKind.Close };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Minimize);
        buttons.Children.Add(Maximize);
        buttons.Children.Add(Close);
        var caption = new Border
        {
            Child = buttons,
            BorderThickness = new Thickness(Stroke.Line, 0, 0, 0),
        };
        caption.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        _leading.VerticalAlignment = VerticalAlignment.Center;
        _leading.Margin = new Thickness(Space.S6, 0, Space.S3, 0);
        _trailing.VerticalAlignment = VerticalAlignment.Center;
        _trailing.Margin = new Thickness(0, 0, Space.S3, 0);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(caption, Dock.Right);
        DockPanel.SetDock(_trailing, Dock.Right);
        row.Children.Add(caption);
        row.Children.Add(_trailing);
        row.Children.Add(_leading);
        Child = row;
        foreach (var button in new UIElement[] { Minimize, Maximize, Close, _trailing })
        {
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        }
    }

    /// <summary>The minimize button.</summary>
    public CaptionButton Minimize { get; }

    /// <summary>The maximize or restore button.</summary>
    public CaptionButton Maximize { get; }

    /// <summary>The close button.</summary>
    public CaptionButton Close { get; }

    /// <summary>What goes at the start of the bar: the logo and the path. It drags the window.</summary>
    public object? Leading
    {
        get => _leading.Content;
        set => _leading.Content = value;
    }

    /// <summary>What goes before the buttons (the language); it takes touches, it does not drag the window.</summary>
    public object? Trailing
    {
        get => _trailing.Content;
        set => _trailing.Content = value;
    }

    /// <summary>An element of the window that resizes it from its bottom right corner (the grip of the status bar).</summary>
    public FrameworkElement? ResizeGrip { get; set; }

    /// <summary>
    /// Makes <paramref name="window"/> a real Windows window with this bar (D13): its chrome, the buttons, Snap, the
    /// resize edges and the maximized overhang. Call it once, before the window is shown.
    /// </summary>
    /// <param name="window">The window; this bar is in its content.</param>
    /// <param name="close">What the close button does (the Control Center hides itself).</param>
    public void Attach(Window window, Action close)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(close);
        if (_window is not null)
        {
            throw new InvalidOperationException("The title bar is already attached to a window.");
        }

        _window = window;
        _close = close;
        WindowChrome.SetWindowChrome(
            window,
            new WindowChrome
            {
                CaptionHeight = ComponentSize.TitleBar - Stroke.Line,
                ResizeBorderThickness = new Thickness(Space.S2),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false,
            }
        );
        Minimize.Click += (_, _) => SystemCommands.MinimizeWindow(window);
        Maximize.Click += (_, _) => ToggleMaximized();
        Close.Click += (_, _) => _close();
        window.StateChanged += (_, _) => ApplyState();
        window.SizeChanged += (_, _) => KeepInsideWhenMaximized();
        window.LocationChanged += (_, _) => KeepInsideWhenMaximized();
        // After WPF's and WindowChrome's own hooks, so this one sees the messages first.
        window.Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                TitleBarHook.Attach(source, this);
            }
        };
        ApplyState();
    }

    /// <summary>Names the three buttons for UI Automation (they change with the language).</summary>
    /// <param name="minimize">[min].</param>
    /// <param name="maximize">[winMaximize].</param>
    /// <param name="restore">[winRestore].</param>
    /// <param name="close">[close].</param>
    public void SetNames(string minimize, string maximize, string restore, string close)
    {
        _maximizeName = maximize;
        _restoreName = restore;
        AutomationProperties.SetName(Minimize, minimize);
        AutomationProperties.SetName(Close, close);
        ApplyState();
    }

    /// <summary>Maximizes the window, or restores it when it is maximized.</summary>
    internal void ToggleMaximized()
    {
        if (_window is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(window);
        }
        else
        {
            SystemCommands.MaximizeWindow(window);
        }
    }

    private void ApplyState()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        Maximize.IsMaximized = maximized;
        AutomationProperties.SetName(Maximize, maximized ? _restoreName : _maximizeName);
        KeepInsideWhenMaximized();
    }

    /// <summary>
    /// A maximized window of this chrome is as large as the monitor plus the resize edges Windows puts outside it: the
    /// window then keeps its content inside the work area, on any monitor and at any scale.
    /// </summary>
    private void KeepInsideWhenMaximized()
    {
        if (_window is not { } window)
        {
            return;
        }

        window.BorderThickness =
            window.WindowState == WindowState.Maximized
                ? TitleBarHook.Overhang(window)
                : new Thickness(0);
    }
}
