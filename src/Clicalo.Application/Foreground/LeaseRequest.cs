using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>A request for a foreground lease (blueprint §3.6).</summary>
/// <param name="Kind">The lease kind; it fixes the restoration policy.</param>
/// <param name="Target">The window that must come to the foreground (a surface, the Control Center, the «Try now» app or <c>TrayMenuHost</c>).</param>
/// <param name="Origin">What triggered the request; it selects the rights ladder.</param>
/// <param name="IdleTimeout">
/// The lease ends after this long without interaction; null means the default of the kind
/// (<c>Timings.Foreground.TextInputLeaseIdle</c> for <see cref="LeaseKind.TextInput"/>, none for the others).
/// </param>
public sealed record LeaseRequest(
    LeaseKind Kind,
    WindowToken Target,
    LeaseOrigin Origin,
    TimeSpan? IdleTimeout
);
