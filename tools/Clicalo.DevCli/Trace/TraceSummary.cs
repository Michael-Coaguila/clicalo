namespace Clicalo.DevCli.Trace;

/// <summary>The counts of a traceability report; the final line of <c>trace</c> says them.</summary>
/// <param name="Requirements">Requirements of the catalog, the withdrawn ones included, without edge cases.</param>
/// <param name="Must">MUST requirements that count for this version (neither withdrawn nor put off).</param>
/// <param name="MustTested">Of those, the ones with at least one test.</param>
/// <param name="MustManualOnly">Of those, the ones without a test that the manual acceptance script names.</param>
/// <param name="MustUncovered">Of those, the ones with neither.</param>
/// <param name="UnknownTraits">Traits whose identifier is not in the catalog.</param>
internal sealed record TraceSummary(
    int Requirements,
    int Must,
    int MustTested,
    int MustManualOnly,
    int MustUncovered,
    int UnknownTraits
);
