using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Threading;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// UI Automation peer of <see cref="ShortcutTile"/> (blueprint §8.6, ACC-001, REG-06): control type Button, the name
/// with the voice number (UIA001), the key combination as help text, the state as item status and exactly the
/// pattern of <see cref="ShortcutTile.Pattern"/> (UIA003). Invoking never activates the window (UIA009, S3): the
/// provider methods only raise the tile's events, and the peer never focuses the tile outside a keyboard lease.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>
/// <c>Invoke</c> returns at once and raises <see cref="ShortcutTile.Invoked"/> on the next dispatcher turn, as UI
/// Automation requires (a handler that acquires a foreground lease must not block Voice access), followed by the
/// <c>InvokePatternOnInvoked</c> event.
/// </description></item>
/// <item><description>
/// <c>Toggle</c>, <c>Expand</c> and <c>Collapse</c> raise their events synchronously, so a client that reads the state
/// right after the call sees the one the view model set.
/// </description></item>
/// <item><description>
/// The tile is a leaf: the texts of its template are not UI Automation children, so «clic Negrita» finds exactly
/// one element. It is a control and content element only while it is visible (WPF's own rule, kept on purpose): a
/// collapsed or hidden tile must not get a voice number from «mostrar números» nor answer «clic Negrita».
/// </description></item>
/// <item><description>
/// <c>IsKeyboardFocusable</c> is true only while the tile's window is active (a <c>TextInput</c> or
/// <c>KeyboardNavigation</c> lease); otherwise <c>SetFocus</c> fails with <see cref="InvalidOperationException"/>
/// instead of focusing, because focusing an element activates its window (REG-01).
/// </description></item>
/// <item><description>
/// The secondary action (CUA-014, CUA-015) is the tile's right click: WPF has no UI Automation <c>ShowContextMenu</c>,
/// so «clic derecho {nombre}» of Voice access, a right click, the Menu key and Shift+F10 reach
/// <see cref="ShortcutTile.SecondaryRequested"/> through <c>ContextMenuOpening</c>, and the panel opens the same menu
/// as the long press.
/// </description></item>
/// <item><description>
/// Changes of the name, the states, the help text, the item status and the pattern raise the matching property
/// changed events, so a client that cached them (Voice access numbers) stays in sync.
/// </description></item>
/// </list>
/// </remarks>
public sealed class ShortcutTileAutomationPeer(ShortcutTile owner)
    : FrameworkElementAutomationPeer(owner),
        IInvokeProvider,
        IToggleProvider,
        IExpandCollapseProvider
{
    private ShortcutTile Tile => (ShortcutTile)Owner;

    /// <inheritdoc />
    public ToggleState ToggleState => Tile.ToggleState;

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState => ToExpandCollapseState(Tile.IsExpanded);

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        Supports(Tile.Pattern, patternInterface) ? this : base.GetPattern(patternInterface);

    /// <inheritdoc />
    public void Invoke()
    {
        EnsureEnabled();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(CompleteInvoke));
    }

    /// <inheritdoc />
    public void Toggle()
    {
        EnsureEnabled();
        Tile.RaiseToggled();
    }

    /// <inheritdoc />
    public void Expand()
    {
        EnsureEnabled();
        Tile.RaiseExpandCollapseRequested(true);
    }

    /// <inheritdoc />
    public void Collapse()
    {
        EnsureEnabled();
        Tile.RaiseExpandCollapseRequested(false);
    }

    /// <summary>Raises the name change after the voice number or the accessible name changed.</summary>
    internal void RaiseNameChanged(string oldName, string newName)
    {
        if (
            HasExplicitValue(AutomationProperties.NameProperty)
            || string.Equals(oldName, newName, StringComparison.Ordinal)
        )
        {
            return;
        }

        RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, newName);
    }

    /// <summary>Raises the availability changes of the patterns after <see cref="ShortcutTile.Pattern"/> changed.</summary>
    internal void RaisePatternChanged(
        ShortcutTilePattern oldPattern,
        ShortcutTilePattern newPattern
    )
    {
        if (oldPattern == newPattern)
        {
            return;
        }

        RaisePropertyChangedEvent(Availability(oldPattern), true, false);
        RaisePropertyChangedEvent(Availability(newPattern), false, true);
    }

    /// <summary>Raises the toggle state change of a Toggle tile.</summary>
    internal void RaiseToggleStateChanged(ToggleState oldState, ToggleState newState)
    {
        if (Tile.Pattern == ShortcutTilePattern.Toggle && oldState != newState)
        {
            RaisePropertyChangedEvent(
                TogglePatternIdentifiers.ToggleStateProperty,
                oldState,
                newState
            );
        }
    }

    /// <summary>Raises the expand and collapse state change of an ExpandCollapse tile.</summary>
    internal void RaiseExpandCollapseStateChanged(bool wasExpanded, bool isExpanded)
    {
        if (Tile.Pattern == ShortcutTilePattern.ExpandCollapse && wasExpanded != isExpanded)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                ToExpandCollapseState(wasExpanded),
                ToExpandCollapseState(isExpanded)
            );
        }
    }

    /// <summary>Raises the change of a text property (help text or item status) unless it is set explicitly.</summary>
    internal void RaiseTextPropertyChanged(
        AutomationProperty property,
        DependencyProperty explicitOverride,
        string? oldValue,
        string? newValue
    )
    {
        if (
            HasExplicitValue(explicitOverride)
            || string.Equals(oldValue, newValue, StringComparison.Ordinal)
        )
        {
            return;
        }

        RaisePropertyChangedEvent(property, oldValue ?? string.Empty, newValue ?? string.Empty);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Button;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(ShortcutTile);

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var explicitName = AutomationProperties.GetName(Tile);
        return string.IsNullOrEmpty(explicitName) ? Tile.AutomationName : explicitName;
    }

    /// <inheritdoc />
    protected override string GetHelpTextCore()
    {
        var explicitHelp = AutomationProperties.GetHelpText(Tile);
        return string.IsNullOrEmpty(explicitHelp)
            ? Tile.AccessibleHelpText ?? string.Empty
            : explicitHelp;
    }

    /// <inheritdoc />
    protected override string GetItemStatusCore()
    {
        var explicitStatus = AutomationProperties.GetItemStatus(Tile);
        return string.IsNullOrEmpty(explicitStatus)
            ? Tile.AccessibleState ?? string.Empty
            : explicitStatus;
    }

    /// <summary>The tile is a leaf: its template texts are not separate UI Automation elements.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    /// <summary>
    /// Keyboard focus is possible only while the tile's window is active (a lease activated it); a
    /// non-activatable surface cannot receive keyboard focus without being activated (REG-01).
    /// </summary>
    protected override bool IsKeyboardFocusableCore() =>
        Tile.Focusable && Tile.IsEnabled && Window.GetWindow(Tile) is { IsActive: true };

    /// <summary>
    /// Focuses the tile only when <see cref="IsKeyboardFocusableCore"/> allows it, with <see cref="Keyboard.Focus"/>
    /// inside the already active window; otherwise fails as UI Automation expects, without touching the focus.
    /// </summary>
    protected override void SetFocusCore()
    {
        if (!IsKeyboardFocusableCore())
        {
            throw new InvalidOperationException(
                "A tile of a non-activatable surface only takes keyboard focus during a keyboard lease."
            );
        }

        if (!ReferenceEquals(Keyboard.Focus(Tile), Tile))
        {
            throw new InvalidOperationException("The tile did not accept the keyboard focus.");
        }
    }

    private static bool Supports(ShortcutTilePattern pattern, PatternInterface patternInterface) =>
        (patternInterface, pattern) switch
        {
            (PatternInterface.Invoke, ShortcutTilePattern.Invoke) => true,
            (PatternInterface.Toggle, ShortcutTilePattern.Toggle) => true,
            (PatternInterface.ExpandCollapse, ShortcutTilePattern.ExpandCollapse) => true,
            _ => false,
        };

    private static AutomationProperty Availability(ShortcutTilePattern pattern) =>
        pattern switch
        {
            ShortcutTilePattern.Invoke =>
                AutomationElementIdentifiers.IsInvokePatternAvailableProperty,
            ShortcutTilePattern.Toggle =>
                AutomationElementIdentifiers.IsTogglePatternAvailableProperty,
            ShortcutTilePattern.ExpandCollapse =>
                AutomationElementIdentifiers.IsExpandCollapsePatternAvailableProperty,
            _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, message: null),
        };

    private static ExpandCollapseState ToExpandCollapseState(bool isExpanded) =>
        isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    private bool HasExplicitValue(DependencyProperty property) =>
        !string.IsNullOrEmpty(Tile.GetValue(property) as string);

    private void CompleteInvoke()
    {
        if (!Tile.IsEnabled)
        {
            return;
        }

        Tile.RaiseInvoked();
        if (ListenerExists(AutomationEvents.InvokePatternOnInvoked))
        {
            RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked);
        }
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }
    }
}
