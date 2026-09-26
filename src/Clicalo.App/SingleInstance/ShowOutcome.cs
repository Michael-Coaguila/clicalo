namespace Clicalo.App.SingleInstance;

/// <summary>What a second start of Clícalo achieved (SIS-003).</summary>
internal enum ShowOutcome
{
    /// <summary>The running instance showed its panel.</summary>
    Shown,

    /// <summary>The running instance answered but refused.</summary>
    Refused,

    /// <summary>
    /// The pipe belongs to a process that is not Clícalo (<c>ipc.squat_detected</c>): nothing was sent (ADR-0010).
    /// </summary>
    Squatted,

    /// <summary>No answer in time (the instance is starting, closing or hung).</summary>
    Unreachable,
}
