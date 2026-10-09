using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The state of the session, the interaction and the foreground that the body of the panel shows (docs/04 §3, §7–§11),
/// as one immutable value the composition passes to <see cref="PanelViewModel.ApplyContext"/>. Its owners are the
/// <c>SessionStore</c>, the <c>InteractionStore</c> and the foreground coordinator (blueprint §6.4); the view model only
/// shows it.
/// </summary>
/// <param name="Frequents">Frequents is in view (the tiles are its list; ★ is filled and the button shows ↶).</param>
/// <param name="SearchingWithText">The search is open with text (PAN-008).</param>
/// <param name="PickerOpen">The profile grid is open (SEL-002).</param>
/// <param name="ActiveAppProfile">The profile of the foreground app, for its dot (SEL-001, SEL-003).</param>
/// <param name="SuggestionApp">
/// The foreground app without a profile when a template exists for it: the grid offers «Crear para {app}» (SEL-003).
/// </param>
/// <param name="ElevatedApp">
/// The foreground app while it is elevated and Clícalo is not (EJE-013); <see langword="null"/> otherwise or when it
/// cannot be known.
/// </param>
/// <param name="Notice">The notice on show (AVI-001), or <see langword="null"/> at rest.</param>
/// <param name="CanRepeat">There is a last action to repeat (AVI-004).</param>
/// <param name="EditMode">The panel is in edit mode (CUA-012).</param>
/// <param name="AddTile">
/// The dashed «+ [add]» tile ends the list (edit mode, CUA-012): it takes one slot of the last page.
/// </param>
public sealed record PanelBodyContext(
    bool Frequents = false,
    bool SearchingWithText = false,
    bool PickerOpen = false,
    ProfileId? ActiveAppProfile = null,
    string? SuggestionApp = null,
    string? ElevatedApp = null,
    PanelNotice? Notice = null,
    bool CanRepeat = false,
    bool EditMode = false,
    bool AddTile = false
)
{
    /// <summary>The context of a new panel: a profile in view, nothing open, nothing to say.</summary>
    public static PanelBodyContext Idle { get; } = new();
}
