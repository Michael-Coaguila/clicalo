namespace Clicalo.Platform.Core.Injection;

/// <summary>Whether the gate let an effect through (blueprint §3.2, rule 6).</summary>
public enum GateResult
{
    /// <summary>The generation matched and the effect ran.</summary>
    Ran,

    /// <summary>The generation is old: nothing ran (INV-11).</summary>
    Fenced,
}
