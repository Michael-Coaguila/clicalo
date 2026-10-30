using Clicalo.Domain.Keys;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>What a key did to a recording with the keyboard (EDI-010).</summary>
public abstract record ChordRecording
{
    private ChordRecording() { }

    /// <summary>Only modifiers so far, or a key that is not in the catalog: the recording goes on.</summary>
    public sealed record Waiting : ChordRecording;

    /// <summary>Esc: the recording ends and nothing changes.</summary>
    public sealed record Cancelled : ChordRecording;

    /// <summary>The first key that is not a modifier closed the recording.</summary>
    /// <param name="Chord">The modifiers in the order they were pressed, with their side, and the key.</param>
    public sealed record Recorded(KeyChord Chord) : ChordRecording;
}
