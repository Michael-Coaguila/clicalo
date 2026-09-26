using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// Every live <see cref="NonActivatingWindow"/> of the process, with the <see cref="OwnerAnchor"/> that owns them and
/// the <see cref="ActivationGuard"/> that watches them (blueprint §3.5). Written on the UI thread when a surface's
/// handle is created or destroyed; read from any thread through an immutable snapshot. It is the adapter of
/// <see cref="ISurfaceActivationStyle"/> and <see cref="ISurfaceLookup"/> for <c>ForegroundOrchestrator</c>, and the
/// list that <see cref="SurfaceIntegrityCheck"/> repairs.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class SurfaceRegistry : ISurfaceActivationStyle, ISurfaceLookup
{
    /// <summary>Creates the registry of one UI thread.</summary>
    /// <param name="anchor">Hidden owner of every surface.</param>
    /// <param name="guard">Runtime REG-01 guard fed by every surface.</param>
    public SurfaceRegistry(OwnerAnchor anchor, ActivationGuard guard)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(guard);
        Anchor = anchor;
        Guard = guard;
    }

    /// <summary>Hidden owner of every surface.</summary>
    public OwnerAnchor Anchor { get; }

    /// <summary>Runtime REG-01 guard fed by every surface.</summary>
    public ActivationGuard Guard { get; }

    /// <summary>The registered surfaces, in registration order.</summary>
    public ImmutableArray<NonActivatingWindow> Surfaces =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>
    /// Registers <paramref name="surface"/> once its handle exists (called by <see cref="NonActivatingWindow"/>
    /// itself). Throws if another live surface has the same id.
    /// </summary>
    public void Register(NonActivatingWindow surface) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>Removes <paramref name="surface"/> when its handle is destroyed.</summary>
    public void Unregister(NonActivatingWindow surface) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>
    /// True while a <c>TextInput</c> or <c>KeyboardNavigation</c> lease lets <paramref name="surface"/> activate
    /// (between <see cref="AllowActivation"/> and <see cref="RestoreNoActivate"/>).
    /// </summary>
    public bool IsActivationAllowed(SurfaceId surface) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <inheritdoc />
    public void AllowActivation(SurfaceId surface) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <inheritdoc />
    public void RestoreNoActivate(SurfaceId surface) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <inheritdoc />
    public bool TryGetSurface(WindowToken window, out SurfaceId surface) =>
        throw new NotImplementedException("M1 windowing package.");
}
