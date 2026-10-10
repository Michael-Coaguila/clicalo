using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution;

/// <summary>
/// A scroll action repeating while its contact lasts (EJE-009): one wheel step every <c>Timings.Mouse.ScrollRepeat*</c>
/// of its speed, faster after every <c>Timings.Engine.ScrollAccelerationEvery</c> steps down to
/// <c>Timings.Engine.ScrollRepeatFloor</c>. It holds no key, but its contact has an item in the ledger with
/// <c>HoldOrigin.Dock</c> (SEG-001), so the panic strip shows it; its end is the end of the
/// contact, «Release all», a terminal event or the global automatic release limit.
/// </summary>
/// <param name="ContactId">The contact that holds it.</param>
/// <param name="Op">The scroll direction.</param>
/// <param name="Speed">The speed.</param>
/// <param name="Origin">The execution (target point and epoch).</param>
/// <param name="SinceTicks">When it started (the global limit counts from here, SEG-004).</param>
/// <param name="Steps">Wheel steps sent so far.</param>
/// <param name="NextTicks">When the next step goes.</param>
/// <param name="DeadlineTicks">When it stops by itself, or <see langword="null"/> for «Never».</param>
public sealed record ScrollRepeat(
    int ContactId,
    MouseOp Op,
    ScrollSpeed Speed,
    ExecutionOrigin Origin,
    long SinceTicks,
    int Steps,
    long NextTicks,
    long? DeadlineTicks
);
