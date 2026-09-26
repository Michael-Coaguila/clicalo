namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>One change of the foreground window, as the laboratory saw it (no title: LOG-001).</summary>
/// <param name="Window">The new foreground window.</param>
/// <param name="ProcessId">Its process.</param>
/// <param name="ProcessName">Its process name («notepad», «WINWORD»…).</param>
/// <param name="At">When the laboratory saw it.</param>
internal sealed record ForegroundChange(
    nint Window,
    uint ProcessId,
    string ProcessName,
    DateTimeOffset At
)
{
    /// <summary>The name of the lab surface, when the new foreground is one («Panel#0»…).</summary>
    public string? Surface { get; init; }

    /// <summary>True when the window belongs to the laboratory's own process.</summary>
    public bool IsOwnProcess { get; init; }

    /// <summary>True when the window is InputProbe.</summary>
    public bool IsProbe { get; init; }

    /// <summary>How the maintainer reads it: the process, or «SpikeLab · Panel#0».</summary>
    public string Describe() => Surface is { } surface ? "SpikeLab · " + surface : ProcessName;
}
