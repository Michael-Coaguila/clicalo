namespace Clicalo.Domain.Library;

/// <summary>A mouse action at the last pointer position outside Clícalo (EJE-009, blueprint §7.11).</summary>
/// <param name="Op">The action.</param>
/// <param name="Speed">Repeat speed of the scroll actions; ignored by the others.</param>
public sealed record MouseAction(MouseOp Op, ScrollSpeed Speed) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Mouse;
}
