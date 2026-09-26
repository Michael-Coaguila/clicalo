namespace Clicalo.Performance;

/// <summary>
/// The start-up budgets of blueprint §10.3 and spike S5 (NFR-001): a gate on the touch laboratory, a trend on the
/// hosted runners. The numbers are copied from the plan; changing them is a change of requirement (§6.1 of the catalog).
/// </summary>
internal static class StartupBudgets
{
    /// <summary>First frame of a cold manual start: p50.</summary>
    public static readonly TimeSpan ColdFirstFrameP50 = TimeSpan.FromMilliseconds(700);

    /// <summary>First frame of a cold manual start, and at sign-in: maximum (NFR-001, no exception).</summary>
    public static readonly TimeSpan ColdFirstFrameMax = TimeSpan.FromMilliseconds(1000);

    /// <summary>First frame of a warm start: p50 (trend on hosted runners).</summary>
    public static readonly TimeSpan WarmFirstFrameP50 = TimeSpan.FromMilliseconds(300);

    /// <summary>First frame of a warm start: maximum (trend on hosted runners).</summary>
    public static readonly TimeSpan WarmFirstFrameMax = TimeSpan.FromMilliseconds(450);

    /// <summary>Working set with the four surfaces (M2 has one: the number is a floor).</summary>
    public const long WorkingSetBytes = 120L * 1024 * 1024;

    /// <summary>Touch → <c>SendInput</c>, p95 (NFR-001, M2 exit criterion).</summary>
    public static readonly TimeSpan TouchToSendInputP95 = TimeSpan.FromMilliseconds(50);
}
