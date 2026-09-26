namespace Clicalo.Application.Ports;

/// <summary>
/// A surface was activated without a lease: a REG-01 violation detected by <c>ActivationGuard</c> from the surface's
/// own activation messages (blueprint §3.5). Contains no title or user content (LOG-001).
/// </summary>
/// <param name="Surface">The activated surface.</param>
/// <param name="Window">Its window.</param>
/// <param name="Message">The message that revealed the activation.</param>
/// <param name="ProbableCause">The most likely cause, for the log and the metric.</param>
/// <param name="DetectedAt">When the guard saw it, from the guard's <see cref="TimeProvider"/>.</param>
public sealed record ActivationViolation(
    SurfaceId Surface,
    WindowToken Window,
    ActivationMessage Message,
    ActivationCause ProbableCause,
    DateTimeOffset DetectedAt
);
