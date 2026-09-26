using Clicalo.Tools.SpikeLab.Input;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tiles;

/// <summary>
/// One tile of a laboratory surface: a real <c>ShortcutTile</c> with its accessible name, glyph, UI Automation
/// pattern and action.
/// </summary>
/// <param name="Id">Stable identifier, used by the scripts and the report.</param>
/// <param name="Name">Accessible and visible name (never a glyph, UIA008).</param>
/// <param name="Glyph">Decorative glyph drawn above the name; empty for none.</param>
/// <param name="Pattern">The UI Automation pattern of the tile.</param>
/// <param name="Action">What it does.</param>
internal sealed record LabTile(
    string Id,
    string Name,
    string Glyph,
    CommandPattern Pattern,
    LabAction Action
)
{
    /// <summary>The chord of a <see cref="LabAction.SendChord"/> tile.</summary>
    public LabChord? Chord { get; init; }

    /// <summary>
    /// True for the tiles of the surfaces under test: their taps and commands count as repetitions. The guide strip
    /// and the search surface are instruments, not subjects.
    /// </summary>
    public bool IsTestTarget { get; init; } = true;

    /// <summary>The name followed by the chord, for the «Última orden» line and the report.</summary>
    public string Describe() => Chord is { } chord ? Name + " (" + chord.Describe() + ")" : Name;
}
