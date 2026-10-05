using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>Press the combination in press order and release it in reverse order (EJE-003).</summary>
/// <param name="Chord">The combination; empty means incomplete.</param>
/// <param name="Variants">Combinations for other apps languages (templates).</param>
public sealed record TapAction(KeyChord Chord, ValueList<ChordVariant> Variants) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Tap;
}
