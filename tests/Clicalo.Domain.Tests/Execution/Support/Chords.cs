using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>Builds combinations for the engine tests through <see cref="KeyChord.Create"/>.</summary>
internal static class Chords
{
    /// <summary>A chord of the given keys, in press order: <c>Of("ctrl", "c")</c>.</summary>
    public static KeyChord Of(params string[] keys) =>
        Of([.. keys.Select(static k => new KeyStroke(new KeyId(k)))]);

    /// <summary>A chord of the given strokes, in press order.</summary>
    public static KeyChord Of(params KeyStroke[] strokes) => KeyChord.Create(strokes);
}
