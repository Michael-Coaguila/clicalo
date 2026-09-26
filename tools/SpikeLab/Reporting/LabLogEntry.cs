namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// One event of the laboratory's timeline (a tap, a foreground change, an activation message, a lease, a refused
/// injection…). The detail never contains a window title or text the maintainer typed.
/// </summary>
/// <param name="At">When it happened.</param>
/// <param name="Kind">Short machine-readable kind (<c>tap</c>, <c>foreground</c>, <c>activation</c>, <c>violation</c>…).</param>
/// <param name="Detail">One readable sentence.</param>
internal sealed record LabLogEntry(DateTimeOffset At, string Kind, string Detail);
