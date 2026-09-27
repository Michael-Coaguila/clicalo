namespace Clicalo.Performance;

/// <summary>Where a budget of <c>data/catalogs/budgets.json</c> fails the run instead of only being reported.</summary>
internal enum BudgetGate
{
    /// <summary>Every continuous-integration run that measures it: the hosted runners and the touch laboratory.</summary>
    EveryRun,

    /// <summary>Only the touch laboratory (<c>CLICALO_PERF_GATE=1</c>); a trend everywhere else.</summary>
    TouchLab,
}
