namespace Clicalo.Application.UseCases.Editor;

/// <summary>The phase line of the «Probar» card (PRB-001, PRB-002); each maps to one text and one icon.</summary>
public enum PlaybackPhase
{
    /// <summary>Nothing played yet.</summary>
    None,

    /// <summary>Press: the keys go down one by one.</summary>
    Pressing,

    /// <summary>Press: [phReleasedAll].</summary>
    ReleasedAll,

    /// <summary>Hold: [phTouch].</summary>
    Touch,

    /// <summary>Hold: [phHolding].</summary>
    Holding,

    /// <summary>Hold: [phLift].</summary>
    Lift,

    /// <summary>Toggle: [phTap1].</summary>
    FirstTap,

    /// <summary>Toggle: [phLatched].</summary>
    Latched,

    /// <summary>Toggle: [phTap2].</summary>
    SecondTap,

    /// <summary>Toggle: [phReleased].</summary>
    Released,

    /// <summary>Macro: [phStep] with the step.</summary>
    Step,

    /// <summary>Text, Web, App and Mouse: their [tw*] sentence.</summary>
    Sentence,

    /// <summary>[phDone].</summary>
    Done,
}
