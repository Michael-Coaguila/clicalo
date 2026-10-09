using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>What the editor column of «Atajos» shows (docs/05 §1): one state at a time.</summary>
public abstract record EditorPane
{
    private EditorPane() { }

    /// <summary>C. Nothing selected: [pickOne] (EDI-020).</summary>
    public sealed record Empty : EditorPane;

    /// <summary>A. «Añadir atajo»: «Crear el mío» and the library (ATJ-010).</summary>
    public sealed record Library : EditorPane;

    /// <summary>B. The editor of a shortcut of the document.</summary>
    /// <param name="Id">The shortcut.</param>
    public sealed record Editing(ShortcutId Id) : EditorPane;

    /// <summary>
    /// B. The editor of a draft of «Crear el mío» that is not in the document yet (ATJ-011): its first meaningful edit
    /// creates it; leaving it blank leaves no trace.
    /// </summary>
    /// <param name="Shortcut">The draft.</param>
    public sealed record Draft(Shortcut Shortcut) : EditorPane;
}
