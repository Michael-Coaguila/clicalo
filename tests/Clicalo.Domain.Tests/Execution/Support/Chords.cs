using System.Reflection;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// Builds combinations for the engine tests. <see cref="KeyChord.Create"/> belongs to the domain package; until it is
/// integrated (docs/testing/spikes/M2-ownership.md: «KeyChord.Create con un doble local en las pruebas») this local
/// double builds the chord exactly as given, already normalized by the test itself. Once <c>Create</c> works, the
/// tests go through it automatically.
/// </summary>
internal static class Chords
{
    private static readonly Lazy<bool> CreateIsImplemented = new(static () =>
    {
        try
        {
            _ = KeyChord.Create([new KeyStroke(KeyIds.A)]);
            return true;
        }
        catch (NotImplementedException)
        {
            return false;
        }
    });

    /// <summary>A chord of the given keys, in press order: <c>Of("ctrl", "c")</c>.</summary>
    public static KeyChord Of(params string[] keys) =>
        Of([.. keys.Select(static k => new KeyStroke(new KeyId(k)))]);

    /// <summary>A chord of the given strokes, in press order.</summary>
    public static KeyChord Of(params KeyStroke[] strokes)
    {
        if (CreateIsImplemented.Value)
        {
            return KeyChord.Create(strokes);
        }

        var constructor = typeof(KeyChord).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(ValueList<KeyStroke>)]
        )!;
        return (KeyChord)constructor.Invoke([new ValueList<KeyStroke>([.. strokes])]);
    }
}
