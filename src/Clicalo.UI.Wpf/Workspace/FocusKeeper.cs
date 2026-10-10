using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// Keeps the keyboard focus when a section draws itself again (ACC-004): the views of the Control Center rebuild a
/// region whenever its model changes, which removes the control that had the keyboard. This remembers which control it
/// was (its kind, its accessible name and its place in the tab order) and, once the new region is in the window, gives
/// the keyboard to the control that took its place, so keyboard and switch users keep going where they were.
/// </summary>
public sealed class FocusKeeper
{
    private readonly Window _window;
    private DependencyObject? _last;
    private FocusMark? _mark;
    private bool _pending;

    private FocusKeeper(Window window)
    {
        _window = window;
        window.AddHandler(
            Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnGotFocus),
            handledEventsToo: true
        );
        window.LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>Follows the keyboard focus inside <paramref name="window"/>.</summary>
    /// <param name="window">The window whose sections rebuild themselves.</param>
    public static FocusKeeper Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new FocusKeeper(window);
    }

    /// <summary>The focusable controls under <paramref name="root"/>, in the order of the tree.</summary>
    /// <param name="root">The window or a part of it.</param>
    public static IReadOnlyList<Control> Stops(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var stops = new List<Control>();
        Collect(root, stops);
        return stops;
    }

    /// <summary>What to remember of <paramref name="control"/> to find it again after its region is rebuilt.</summary>
    /// <param name="root">The window or a part of it.</param>
    /// <param name="control">The control with the keyboard.</param>
    public static FocusMark Mark(DependencyObject root, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return new FocusMark(
            control.GetType(),
            AutomationProperties.GetName(control) ?? string.Empty,
            Stops(root).ToList().IndexOf(control)
        );
    }

    /// <summary>
    /// The control that took the place of the one <paramref name="mark"/> describes: the nearest one of the same kind
    /// and name, or the one at the same place in the order when the name changed (an armed button).
    /// </summary>
    /// <param name="root">The window or a part of it.</param>
    /// <param name="mark">What was remembered.</param>
    public static Control? Find(DependencyObject root, FocusMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var stops = Stops(root);
        if (stops.Count == 0)
        {
            return null;
        }

        Control? best = null;
        var distance = int.MaxValue;
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            if (
                stop.GetType() == mark.Kind
                && mark.Name.Length > 0
                && string.Equals(
                    AutomationProperties.GetName(stop),
                    mark.Name,
                    StringComparison.Ordinal
                )
                && Math.Abs(i - mark.Ordinal) < distance
            )
            {
                best = stop;
                distance = Math.Abs(i - mark.Ordinal);
            }
        }

        return best ?? stops[Math.Clamp(mark.Ordinal, 0, stops.Count - 1)];
    }

    private static void Collect(DependencyObject node, List<Control> stops)
    {
        if (node is UIElement { Visibility: not Visibility.Visible })
        {
            return;
        }

        if (node is Control { Focusable: true, IsTabStop: true, IsEnabled: true } control)
        {
            stops.Add(control);
            if (control is TextBox or PasswordBox or Slider)
            {
                return;
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is DependencyObject element)
            {
                Collect(element, stops);
            }
        }
    }

    private bool IsConnected(DependencyObject element)
    {
        var node = element;
        while (node is not null)
        {
            if (ReferenceEquals(node, _window))
            {
                return true;
            }

            node =
                LogicalTreeHelper.GetParent(node)
                ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }

        return false;
    }

    private void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is Control { IsTabStop: true } control && IsConnected(control))
        {
            _last = control;
            _mark = Mark(_window, control);
        }
        else
        {
            _last = null;
            _mark = null;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pending || _last is null || _mark is null || IsConnected(_last))
        {
            return;
        }

        _pending = true;
        _ = _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Restore);
    }

    private void Restore()
    {
        _pending = false;
        if (_last is null || _mark is not { } mark || IsConnected(_last))
        {
            return;
        }

        _last = null;
        _mark = null;

        // The keyboard went to another control in the meantime: the person moved on.
        if (
            !_window.IsActive
            || (Keyboard.FocusedElement is Control { IsTabStop: true } other && IsConnected(other))
        )
        {
            return;
        }

        if (Find(_window, mark) is { } heir)
        {
            _ = Keyboard.Focus(heir);
        }
    }
}
