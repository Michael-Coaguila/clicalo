namespace Clicalo.Application.Ports;

/// <summary>
/// Whether the process behind an external foreground runs elevated (integrity level High or above), which UIPI uses
/// to block Clícalo's input (blueprint §7.9, EJE-013).
/// </summary>
public enum ProcessElevation
{
    /// <summary>The process could not be opened or its token read (a protected process): no false admin notice (EC-PER-03).</summary>
    Unknown,

    /// <summary>Medium integrity or lower: Clícalo's input reaches it.</summary>
    NotElevated,

    /// <summary>High integrity or above: input from a medium-integrity Clícalo is blocked by UIPI.</summary>
    Elevated,
}
