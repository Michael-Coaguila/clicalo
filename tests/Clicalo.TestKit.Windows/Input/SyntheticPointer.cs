using System.Diagnostics.CodeAnalysis;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Test-only pointer injection (blueprint §10.1 <c>SyntheticPointer</c>): a finger or a pen through
/// <c>CreateSyntheticPointerDevice</c> and <c>InjectSyntheticPointerInput</c>, or a mouse through <c>SendInput</c>.
/// It only ever touches windows of the allowed processes (the test process and InputProbe) and refuses to inject
/// anything anywhere else.
/// </summary>
/// <remarks>
/// Safety rules, checked immediately before EVERY injected frame, never only once per gesture:
/// <list type="number">
/// <item>the root window under the exact point (<c>WindowFromPoint</c> + <c>GetAncestor(GA_ROOT)</c>) belongs to one
/// of <see cref="AllowedProcessIds"/>; otherwise <see cref="InjectionRefusedException"/> and nothing is injected;</item>
/// <item>every gesture is one call that always ends its contact (up, or cancel from a <c>finally</c>) and releases
/// the mouse buttons it pressed, so no contact or button can stay down;</item>
/// <item>no keyboard input is ever injected by this class.</item>
/// </list>
/// Desktop tests run it only with <c>CLICALO_DESKTOP_TESTS=1</c>; the hosted CI runners run them systematically.
/// </remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class SyntheticPointer : IDisposable
{
    /// <summary>Creates an injector of <paramref name="kind"/> that may only touch windows of <paramref name="allowedProcessIds"/>.</summary>
    public SyntheticPointer(SyntheticPointerKind kind, IReadOnlyCollection<int> allowedProcessIds)
    {
        ArgumentNullException.ThrowIfNull(allowedProcessIds);
        if (allowedProcessIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one allowed process is required.",
                nameof(allowedProcessIds)
            );
        }

        Kind = kind;
        AllowedProcessIds = allowedProcessIds;
    }

    /// <summary>The impersonated device.</summary>
    public SyntheticPointerKind Kind { get; }

    /// <summary>The only processes whose windows may receive the input.</summary>
    public IReadOnlyCollection<int> AllowedProcessIds { get; }

    /// <summary>
    /// True when the root window under (<paramref name="x"/>, <paramref name="y"/>) belongs to an allowed process;
    /// <paramref name="description"/> names the window's process for failure messages (never its title).
    /// </summary>
    public bool IsAllowedTarget(int x, int y, out string description) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>Down and up at a physical screen point, in one guarded gesture.</summary>
    public void Tap(int x, int y) => throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Down at a physical screen point, updates for <paramref name="duration"/> without moving, then up (a long press
    /// or a hold). The up is sent even if a check fails in between.
    /// </summary>
    public void Hold(int x, int y, TimeSpan duration) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>A straight move from one point to another over <paramref name="duration"/>, down to up.</summary>
    public void Drag(int fromX, int fromY, int toX, int toY, TimeSpan duration) =>
        throw new NotImplementedException("M1 pointer package.");

    /// <summary>Destroys the synthetic device.</summary>
    public void Dispose() { }
}
