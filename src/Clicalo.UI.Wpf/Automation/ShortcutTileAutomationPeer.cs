using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// UI Automation peer of <see cref="ShortcutTile"/> (blueprint §8.6, ACC-001, REG-06): control type Button, the name
/// with the voice number (UIA001) and exactly the pattern of <see cref="ShortcutTile.Pattern"/> (UIA003). Invoking
/// never activates the window (UIA009, S3): the provider methods only raise the tile's events.
/// </summary>
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
    public ExpandCollapseState ExpandCollapseState =>
        Tile.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        (patternInterface, Tile.Pattern) switch
        {
            (PatternInterface.Invoke, ShortcutTilePattern.Invoke) => this,
            (PatternInterface.Toggle, ShortcutTilePattern.Toggle) => this,
            (PatternInterface.ExpandCollapse, ShortcutTilePattern.ExpandCollapse) => this,
            _ => base.GetPattern(patternInterface),
        };

    /// <inheritdoc />
    public void Invoke()
    {
        EnsureEnabled();
        Tile.RaiseInvoked();
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

    /// <summary>A tile is always a control and content element, so voice «mostrar números» numbers it.</summary>
    protected override bool IsControlElementCore() => true;

    private void EnsureEnabled()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }
    }
}
