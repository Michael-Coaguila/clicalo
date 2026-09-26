namespace Clicalo.Domain.Touch;

/// <summary>Result of <see cref="TouchFilter.Evaluate"/> for one finished contact on one target (TAC-002).</summary>
public enum TouchVerdict
{
    /// <summary>The touch counts; its time is stored for the debounce.</summary>
    Accepted,

    /// <summary>It moved further than the cancel distance (step 1).</summary>
    IgnoredSwipe,

    /// <summary>It was shorter than the minimum contact (step 2).</summary>
    IgnoredShort,

    /// <summary>The same target accepted a touch less than the debounce ago (step 3).</summary>
    IgnoredDouble,

    /// <summary>The contact area is palm-sized (ACC-007).</summary>
    IgnoredPalm,
}
