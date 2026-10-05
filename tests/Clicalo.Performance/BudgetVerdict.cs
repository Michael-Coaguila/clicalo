namespace Clicalo.Performance;

/// <summary>What a measurement means for its budget.</summary>
internal enum BudgetVerdict
{
    /// <summary>Within the budget.</summary>
    Within,

    /// <summary>Over the budget where the budget is only a trend: reported, never failing.</summary>
    OverTrend,

    /// <summary>Over the budget where it gates, or measured on too few values: the run fails.</summary>
    Failed,
}
