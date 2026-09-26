namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// Everything the laboratory measured during one repetition. It never contains a window title or text the maintainer
/// typed: only process names, counters, lengths and times (LOG-001).
/// </summary>
internal sealed record RepetitionEvidence
{
    /// <summary>No measurement.</summary>
    public static RepetitionEvidence Empty { get; } = new();

    /// <summary>What the counters did between the start and the end of the repetition.</summary>
    public MeasurementCounters Delta { get; init; }

    /// <summary>The trigger that counted the repetition; null for a manual mark.</summary>
    public TriggerInfo? Trigger { get; init; }

    /// <summary>The process in front at the end of the repetition.</summary>
    public string? ForegroundProcess { get; init; }

    /// <summary>The process that was the target app of the step.</summary>
    public string? TargetProcess { get; init; }

    /// <summary>True when the target app of the step was still in front at the end; null when unknown.</summary>
    public bool? TargetInFront { get; init; }

    /// <summary>Milliseconds from the lift of the finger (or the UI Automation call) to the action being done.</summary>
    public double? LatencyMs { get; init; }

    /// <summary>The lease of a <see cref="StepTrigger.LeaseCycle"/>.</summary>
    public LeaseEvidence? Lease { get; init; }

    /// <summary>Forced activation: milliseconds until the previous foreground was back; null when it never came back.</summary>
    public double? RestoredWithinMs { get; init; }

    /// <summary>Forced activation: whether the panel still had <c>WS_EX_NOACTIVATE</c> afterwards.</summary>
    public bool? NoActivateStyleKept { get; init; }

    /// <summary>Text fields of the cycle (the search field, or the three fields of the lab Control Center).</summary>
    public int FieldCount { get; init; }

    /// <summary>How many of those fields received text (lengths only).</summary>
    public int FieldsWithText { get; init; }
}
