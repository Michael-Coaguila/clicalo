using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Library;

/// <summary>Each accepted tap latches the combination on or releases it (EJE-007).</summary>
/// <param name="Chord">The combination; empty means incomplete.</param>
public sealed record ToggleAction(KeyChord Chord) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Toggle;
}
