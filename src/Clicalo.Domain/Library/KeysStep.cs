using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Library;

/// <summary>Press and release a combination (one chord of a v1 multi-chord hotkey, catalog §7.4).</summary>
/// <param name="Chord">The combination.</param>
public sealed record KeysStep(KeyChord Chord) : MacroStep;
