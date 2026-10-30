namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>The background of a «Toca aquí» target (TAC-006): its last touch.</summary>
public enum TestMark
{
    /// <summary>No touch yet: card.</summary>
    None,

    /// <summary>The last touch passed the filter: accentWash.</summary>
    Registered,

    /// <summary>The last touch was ignored: warnWash.</summary>
    Ignored,
}
