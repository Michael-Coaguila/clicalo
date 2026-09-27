using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Session;

/// <summary>
/// An intention that changes the <see cref="PanelSession"/> (blueprint §6.4, §8.2): the view models and coordinators
/// of the Surfaces role send these to <see cref="SessionStore.Dispatch"/>; they never set the session directly.
/// </summary>
public abstract record SessionAction
{
    private SessionAction() { }

    /// <summary>Show the panel (tray click while hidden, a second start of Clícalo, SIS-003).</summary>
    public sealed record Show : SessionAction;

    /// <summary>Hide the panel (tray menu or tray click while visible, BUR-003).</summary>
    public sealed record Hide : SessionAction;

    /// <summary>Show the panel when hidden and hide it when visible (tray click, BUR-003).</summary>
    public sealed record ToggleVisibility : SessionAction;

    /// <summary>Put another profile in view.</summary>
    /// <param name="Profile">The profile.</param>
    public sealed record ShowProfile(ProfileId Profile) : SessionAction;
}
