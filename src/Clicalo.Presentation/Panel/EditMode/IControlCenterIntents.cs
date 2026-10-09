using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel.EditMode;

/// <summary>
/// What the panel asks of the control center (Workspace role, blueprint §8.1): edit mode, the context menu and Quick
/// settings only emit these intentions; the control center opens with the <c>ControlCenter</c> lease and decides how it
/// shows them (CCM-004). The composition marshals them to the Workspace dispatcher.
/// </summary>
public interface IControlCenterIntents
{
    /// <summary>
    /// A tile tapped in edit mode, or [edit] of its context menu (CUA-012, CUA-014): the control center on Atajos with
    /// that shortcut in the editor. The control center finds its list from the id.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    void OpenEditor(ShortcutId shortcut);

    /// <summary>
    /// The dashed «+ [add]» tile of edit mode (CUA-012): the library, starting on «Para {perfil}» when the profile has a
    /// template.
    /// </summary>
    /// <param name="profile">The profile in view.</param>
    void OpenLibrary(ProfileId profile);

    /// <summary>
    /// The «Centro de control» card of Quick settings (AJR-001): the control center on Atajos with
    /// <paramref name="profile"/> (General while Frequents is in view).
    /// </summary>
    /// <param name="profile">The profile to show.</param>
    void OpenControlCenter(ProfileId profile);
}
