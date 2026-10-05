namespace Clicalo.Domain.Execution;

/// <summary>
/// The fixed chords Clícalo injects for itself, outside any shortcut (blueprint §3.6, D-14): balanced (every key
/// pressed is released in the same batch), never AltGr or right Ctrl, and sent by the engine itself (D-22).
/// </summary>
public enum InternalChord
{
    /// <summary>The reserved rights chord of the foreground ladder: Ctrl+Alt+Shift+F24 with the left modifiers.</summary>
    Rights,

    /// <summary>Win+H, which toggles Windows dictation for the focused field (BUS-003, ACC-011).</summary>
    Dictation,
}
