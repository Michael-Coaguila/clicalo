using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A row of the expanded repeated card (REP-005).</summary>
/// <param name="Id">The appearance.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Category">Its color category.</param>
/// <param name="Name">Its name, in bold.</param>
/// <param name="Where">Its list.</param>
/// <param name="Editing">[editingNow] with a warn border when it is the one in the editor.</param>
/// <param name="DeleteName">[delFrom] {perfil}.</param>
/// <param name="Armed">Whether its delete button was tapped once: [delConfirm] in red.</param>
public sealed record DuplicateRow(
    ShortcutId Id,
    string Icon,
    string Category,
    string Name,
    string Where,
    bool Editing,
    string DeleteName,
    bool Armed
);
