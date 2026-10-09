using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// Where the intentions of the body of the panel go (blueprint §8.2: a VM forwards intentions; it never changes the
/// session, the interaction or the document itself). The composition implements it with session actions, interaction
/// actions, document commands and engine requests.
/// </summary>
public interface IPanelBodyIntents
{
    /// <summary>★ Frequents (SEL-001).</summary>
    void ShowFrequents();

    /// <summary>The profile button from Frequents: back to the return profile (SEL-002, PER-004).</summary>
    void ReturnFromFrequents();

    /// <summary>The profile button outside Frequents: open or close the profile grid (SEL-002).</summary>
    void TogglePicker();

    /// <summary>A tile of the profile grid (SEL-004).</summary>
    /// <param name="profile">The profile chosen.</param>
    void ChooseProfile(ProfileId profile);

    /// <summary>«Crear para {app}» of the profile grid (SEL-003, PER-009).</summary>
    void CreateProfileForActiveApp();

    /// <summary>«+ Más» of the profile grid: the control center on Templates (SEL-003).</summary>
    void OpenTemplates();

    /// <summary>A sticky modifier: 0 → 1 → 2 → 0 (FIJ-005).</summary>
    /// <param name="modifier">Ctrl, Alt, Shift or Win.</param>
    void AdvanceSticky(ModifierKind modifier);

    /// <summary>[undo] of the notice bar (AVI-003).</summary>
    void Undo();

    /// <summary>↻ Repeat of the notice bar (AVI-004).</summary>
    void Repeat();

    /// <summary>«Añadir atajo» of the empty profile card: the editor with a new shortcut (CUA-010).</summary>
    /// <param name="profile">The empty profile.</param>
    void AddShortcut(ProfileId profile);

    /// <summary>[cancel] of a notice that offers it: ends the capture mode of a binding (ATJ-008).</summary>
    void CancelNotice();

    /// <summary>[adminBtn]: relaunch Clícalo elevated, keeping the state (EJE-013).</summary>
    void RelaunchElevated();
}
