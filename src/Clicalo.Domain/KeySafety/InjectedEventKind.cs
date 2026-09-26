namespace Clicalo.Domain.KeySafety;

/// <summary>What an <see cref="InjectedEvent"/> does.</summary>
public enum InjectedEventKind
{
    /// <summary>Press a key.</summary>
    KeyDown,

    /// <summary>Release a key.</summary>
    KeyUp,

    /// <summary>
    /// The menu mask key (<c>VK 0xE8</c>) sent before releasing Alt or Win so neither the Start menu nor a menu bar
    /// opens (blueprint §7.7).
    /// </summary>
    MenuMask,

    /// <summary>Press a mouse button.</summary>
    MouseDown,

    /// <summary>Release a mouse button.</summary>
    MouseUp,
}
