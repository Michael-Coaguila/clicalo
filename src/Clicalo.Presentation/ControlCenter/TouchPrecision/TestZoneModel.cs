using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>The test zone (TAC-006): the column of 340 with the targets, the counters and the last touch.</summary>
/// <param name="Caption">[testZone].</param>
/// <param name="TapHere">[tapHere], on each target.</param>
/// <param name="Marks">The background of each target: its last touch.</param>
/// <param name="Registered">How many touches passed the filter.</param>
/// <param name="RegisteredLabel">[registered].</param>
/// <param name="Ignored">How many touches the filter ignored.</param>
/// <param name="IgnoredLabel">[ignored].</param>
/// <param name="Message">[testIdle], [tOk], [tShort], [tDouble] or «Ignorado: deslizaste».</param>
/// <param name="IsWarning">Whether the last touch was ignored.</param>
/// <param name="ResetLabel">[testReset].</param>
public sealed record TestZoneModel(
    string Caption,
    string TapHere,
    ValueList<TestMark> Marks,
    int Registered,
    string RegisteredLabel,
    int Ignored,
    string IgnoredLabel,
    string Message,
    bool IsWarning,
    string ResetLabel
);
