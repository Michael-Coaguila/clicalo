using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Library;

/// <summary>
/// Keep the combination pressed while the finger stays inside the button's extra area (EJE-004); invoked without a
/// contact (voice, keyboard, switch) it behaves as a toggle (EJE-005).
/// </summary>
/// <param name="Chord">The combination; empty means incomplete.</param>
public sealed record HoldAction(KeyChord Chord) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Hold;
}
