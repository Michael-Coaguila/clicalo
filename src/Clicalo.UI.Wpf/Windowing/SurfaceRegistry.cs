using System.Collections.Immutable;
using System.Windows.Threading;
using Clicalo.Application.Ports;
using Clicalo.UI.Wpf.Windowing.Internal;
using Windows.Win32.Foundation;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// Every live <see cref="NonActivatingWindow"/> of the process, with the <see cref="OwnerAnchor"/> that owns them and
/// the <see cref="ActivationGuard"/> that watches them (blueprint §3.5). Written on the UI thread when a surface's
/// handle is created or destroyed; read from any thread through an immutable snapshot. It is the adapter of
/// <see cref="ISurfaceActivationStyle"/> and <see cref="ISurfaceLookup"/> for <c>ForegroundOrchestrator</c>, and the
/// list that <see cref="SurfaceIntegrityCheck"/> repairs.
/// </summary>
/// <remarks>
/// Create it on the UI thread that will own the surfaces: it keeps that thread's dispatcher.
/// <see cref="AllowActivation"/>, <see cref="RestoreNoActivate"/>, <see cref="IsActivationAllowed"/>,
/// <see cref="TryGetSurface"/> and <see cref="Surfaces"/> may be called from any thread; <see cref="Register"/> and
/// <see cref="Unregister"/> only from the UI thread.
/// </remarks>
public sealed class SurfaceRegistry : ISurfaceActivationStyle, ISurfaceLookup
{
    private RegistryState _state = RegistryState.Empty;

    /// <summary>Creates the registry of one UI thread.</summary>
    /// <param name="anchor">Hidden owner of every surface.</param>
    /// <param name="guard">Runtime REG-01 guard fed by every surface.</param>
    public SurfaceRegistry(OwnerAnchor anchor, ActivationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(guard);
        Anchor = anchor;
        Guard = guard;
        Dispatcher = Dispatcher.CurrentDispatcher;
        Hints = new ActivationHints(Dispatcher);
    }

    /// <summary>Hidden owner of every surface.</summary>
    public OwnerAnchor Anchor { get; }

    /// <summary>Runtime REG-01 guard fed by every surface.</summary>
    public ActivationGuard Guard { get; }

    /// <summary>The registered surfaces, in registration order.</summary>
    public ImmutableArray<NonActivatingWindow> Surfaces => Volatile.Read(ref _state).Surfaces;

    /// <summary>The dispatcher of the UI thread that owns the surfaces.</summary>
    internal Dispatcher Dispatcher { get; }

    /// <summary>What the surfaces were doing when an activation arrived (the probable cause).</summary>
    internal ActivationHints Hints { get; }

    /// <summary>Raised on the UI thread when a surface saw a change after which its styles must be checked.</summary>
    internal event EventHandler? IntegrityCheckRequested;

    /// <summary>
    /// Registers <paramref name="surface"/> once its handle exists (called by <see cref="NonActivatingWindow"/>
    /// itself). Throws if another live surface has the same id.
    /// </summary>
    public void Register(NonActivatingWindow surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Dispatcher.VerifyAccess();
        if (surface.SurfaceWindow.IsNone)
        {
            throw new InvalidOperationException(
                "A surface is registered once its handle exists (OnSourceInitialized)."
            );
        }

        _ = ImmutableInterlocked.Update(
            ref _state,
            static (state, added) => state.With(added),
            surface
        );
    }

    /// <summary>Removes <paramref name="surface"/> when its handle is destroyed.</summary>
    public void Unregister(NonActivatingWindow surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Dispatcher.VerifyAccess();
        _ = ImmutableInterlocked.Update(
            ref _state,
            static (state, removed) => state.Without(removed),
            surface
        );
        Guard.Forget(surface);
    }

    /// <summary>
    /// True while a <c>TextInput</c> or <c>KeyboardNavigation</c> lease lets <paramref name="surface"/> activate
    /// (between <see cref="AllowActivation"/> and <see cref="RestoreNoActivate"/>).
    /// </summary>
    public bool IsActivationAllowed(SurfaceId surface) =>
        Volatile.Read(ref _state).Allowed.Contains(surface);

    /// <inheritdoc />
    public void AllowActivation(SurfaceId surface)
    {
        _ = ImmutableInterlocked.Update(
            ref _state,
            static (state, id) =>
                state.Windows.ContainsKey(id)
                    ? state with
                    {
                        Allowed = state.Allowed.Add(id),
                    }
                    : state,
            surface
        );
        if (Volatile.Read(ref _state).Windows.TryGetValue(surface, out var window))
        {
            _ = SurfaceStyles.SetNoActivate((HWND)window, noActivate: false);
        }
    }

    /// <inheritdoc />
    public void RestoreNoActivate(SurfaceId surface)
    {
        _ = ImmutableInterlocked.Update(
            ref _state,
            static (state, id) => state with { Allowed = state.Allowed.Remove(id) },
            surface
        );
        if (Volatile.Read(ref _state).Windows.TryGetValue(surface, out var window))
        {
            _ = SurfaceStyles.SetNoActivate((HWND)window, noActivate: true);
        }
    }

    /// <inheritdoc />
    public bool TryGetSurface(WindowToken window, out SurfaceId surface) =>
        Volatile.Read(ref _state).ByWindow.TryGetValue(window.Handle, out surface);

    /// <summary>Asks <see cref="SurfaceIntegrityCheck"/> to check every surface after the current message.</summary>
    internal void RequestIntegrityCheck() => IntegrityCheckRequested?.Invoke(this, EventArgs.Empty);
}
