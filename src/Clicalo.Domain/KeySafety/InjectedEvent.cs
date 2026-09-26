using Clicalo.Domain.Keys;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// One key or button event of a batch the engine sends (blueprint §7.4). Releases always carry the same
/// <see cref="InjectedKey"/> as their press (INV-12).
/// </summary>
/// <param name="Kind">What it does.</param>
/// <param name="Key">The key of a key event; default otherwise.</param>
/// <param name="Button">The button of a mouse event; <see cref="MouseButtons.None"/> otherwise.</param>
public readonly record struct InjectedEvent(
    InjectedEventKind Kind,
    InjectedKey Key,
    MouseButtons Button
)
{
    /// <summary>Press <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    public static InjectedEvent KeyDown(InjectedKey key) =>
        new(InjectedEventKind.KeyDown, key, MouseButtons.None);

    /// <summary>Release <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    public static InjectedEvent KeyUp(InjectedKey key) =>
        new(InjectedEventKind.KeyUp, key, MouseButtons.None);

    /// <summary>The menu mask key before releasing Alt or Win.</summary>
    /// <param name="mode">Mode of the release it protects.</param>
    public static InjectedEvent MenuMask(InjectionMode mode) =>
        new(InjectedEventKind.MenuMask, new InjectedKey(0, 0, false, mode), MouseButtons.None);

    /// <summary>Press a mouse button.</summary>
    /// <param name="button">One button.</param>
    public static InjectedEvent MouseDown(MouseButtons button) =>
        new(InjectedEventKind.MouseDown, default, button);

    /// <summary>Release a mouse button.</summary>
    /// <param name="button">One button.</param>
    public static InjectedEvent MouseUp(MouseButtons button) =>
        new(InjectedEventKind.MouseUp, default, button);

    /// <summary>Whether the event releases something (releases are never filtered, INV-8).</summary>
    public bool IsRelease =>
        Kind is InjectedEventKind.KeyUp or InjectedEventKind.MouseUp or InjectedEventKind.MenuMask;
}
