namespace Clicalo.Performance;

/// <summary>How a sample is reduced before it is compared with its budget (nearest-rank percentiles, blueprint §10.3).</summary>
internal enum BudgetStatistic
{
    /// <summary>The median.</summary>
    P50,

    /// <summary>The 95th percentile.</summary>
    P95,

    /// <summary>The largest value.</summary>
    Max,
}
