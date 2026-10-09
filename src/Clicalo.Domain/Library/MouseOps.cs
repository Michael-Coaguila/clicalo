namespace Clicalo.Domain.Library;

/// <summary>Rules of the mouse actions shared by the engine and the panel (EJE-009).</summary>
public static class MouseOps
{
    /// <summary>
    /// Whether <paramref name="op"/> repeats while the finger rests on its tile (the four scroll directions), so the
    /// tile behaves as a Hold.
    /// </summary>
    /// <param name="op">The mouse action.</param>
    public static bool RepeatsWhileHeld(MouseOp op) =>
        op is MouseOp.ScrollUp or MouseOp.ScrollDown or MouseOp.ScrollLeft or MouseOp.ScrollRight;
}
