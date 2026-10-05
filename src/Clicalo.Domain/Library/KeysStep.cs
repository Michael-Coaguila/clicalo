using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Library;

/// <summary>Press and release a combination (one step of a macro, EJE-010).</summary>
/// <param name="Chord">The combination.</param>
public sealed record KeysStep(KeyChord Chord) : MacroStep;
