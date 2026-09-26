namespace Clicalo.Application.Coordinators;

/// <summary>What <see cref="ExitSequence.RunAsync"/> could confirm before the process ends.</summary>
/// <param name="EngineReleased">
/// The engine processed the terminal event and stopped within <c>Timings.App.ExitReleaseWait</c>; otherwise Sentinel
/// releases whatever the ledger still records once the process is gone.
/// </param>
/// <param name="Flushed">The document and usage were written within <c>Timings.App.ExitFlushTimeout</c>.</param>
/// <param name="Elapsed">How long the sequence took.</param>
public sealed record ExitReport(bool EngineReleased, bool Flushed, TimeSpan Elapsed);
