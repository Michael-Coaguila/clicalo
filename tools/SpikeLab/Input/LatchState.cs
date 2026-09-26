namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>The three states of a sticky modifier («Mayús»): the UI Automation Toggle states Off, On and Indeterminate.</summary>
internal enum LatchState
{
    /// <summary>Not latched.</summary>
    Off,

    /// <summary>Latched for the next chord only («activada»).</summary>
    Once,

    /// <summary>Latched until tapped again («bloqueada»).</summary>
    Locked,
}
