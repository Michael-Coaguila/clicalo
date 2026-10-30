namespace Clicalo.Presentation.Panel.Header;

/// <summary>
/// What the header buttons do (CAB-001), given by the composition. A button whose action is <see langword="null"/> is
/// not shown until its part of the panel exists: the prototype has no disabled header button to copy.
/// </summary>
/// <param name="Search">🔍 opens or closes the search (BUS-001).</param>
/// <param name="Edit">✏ enters or leaves edit mode (CUA-012).</param>
/// <param name="QuickSettings">Opens or closes Quick settings (AJR).</param>
/// <param name="Minimize">− turns the panel into the bubble (PAN-001 a).</param>
public sealed record PanelHeaderActions(
    Action? Search,
    Action? Edit,
    Action? QuickSettings,
    Action? Minimize
)
{
    /// <summary>
    /// The title opens or closes the profile grid while the selector row is hidden in the Full view (SEL-006);
    /// <see langword="null"/> leaves the title as a drag zone only.
    /// </summary>
    public Action? Title { get; init; }

    /// <summary>No action yet: only the grip, the title and Auto/Fixed are shown.</summary>
    public static PanelHeaderActions None { get; } = new(null, null, null, null);
}
