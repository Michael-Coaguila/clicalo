namespace Clicalo.Presentation.Dock;

/// <summary>
/// What the handle and the bar of the Tab view ask for (docs/04 «Vista pestaña»), implemented by the composition, which
/// sends each to the session, the interaction store, the document or the engine. The view models only forward them.
/// </summary>
public interface IDockIntents
{
    /// <summary>A tap on the closed handle: the bar opens (PES-002).</summary>
    void OpenBar();

    /// <summary>The handle was dragged along its edge to <paramref name="percent"/> (PES-002).</summary>
    /// <param name="percent">The new position on its edge, 8 to 92.</param>
    void MoveHandle(int percent);

    /// <summary>The chevron of the bar: it folds and its side windows close (PES-005).</summary>
    void CloseBar();

    /// <summary>Expand: the view becomes Full (PAN-001 d).</summary>
    void Expand();

    /// <summary>★ Frequents (PER-005).</summary>
    void ShowFrequents();

    /// <summary>
    /// The profile button: from Frequents back to the profile (PER-004); otherwise the profile grid beside the bar
    /// (PES-006, PES-011).
    /// </summary>
    void ProfileButton();

    /// <summary>The Auto/Fixed pill (PER-006).</summary>
    void ToggleLock();

    /// <summary>🔍 Search: the Full view with an empty search (PAN-001 d, BUS-006).</summary>
    void Search();

    /// <summary>↻ Repeat (AVI-004).</summary>
    void Repeat();

    /// <summary>📌 Pinned: its window beside the button, or closes it (PES-010).</summary>
    void TogglePinned();

    /// <summary>Sticky keys: its window beside the button, or closes it (PES-008, AUD-13).</summary>
    void ToggleSticky();

    /// <summary>The lock of the bar: «Collapses» or «Open» (PES-008, PES-012).</summary>
    void TogglePinOpen();

    /// <summary>The <c>tune</c> button: Quick settings beside the bar, with the side of the Tab view (PES-009).</summary>
    void QuickSettings();

    /// <summary>[next] or [understood] in the guide (PES-015).</summary>
    void CoachNext();

    /// <summary>[coachSkip] in the guide (PES-015).</summary>
    void CoachSkip();

    /// <summary>The floating «Release all» (PES-013, REG-03).</summary>
    void ReleaseAll();
}
