namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>What the filter did with a touch of the test zone (TAC-006).</summary>
public enum TestOutcome
{
    /// <summary>It passed the filter: [tOk].</summary>
    Registered,

    /// <summary>It moved further than «cancelar si deslizas»: «Ignorado: deslizaste».</summary>
    Moved,

    /// <summary>It was shorter than the minimum contact: [tShort].</summary>
    TooShort,

    /// <summary>The same target accepted a touch less than the debounce ago: [tDouble].</summary>
    TooSoon,
}
