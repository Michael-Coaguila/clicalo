namespace Clicalo.Domain.KeySafety;

/// <summary>Why an item is pressed (SEG-001: each item keeps its origin).</summary>
public enum HoldOrigin
{
    /// <summary>A Hold under a finger, pen or mouse contact.</summary>
    Contact,

    /// <summary>A Hold invoked without contact (voice, keyboard, switch), which behaves as a toggle.</summary>
    Invoke,

    /// <summary>A latched Toggle, including a mouse drag.</summary>
    Toggle,

    /// <summary>A sticky modifier.</summary>
    Sticky,

    /// <summary>A running macro.</summary>
    Macro,

    /// <summary>A repeating button of the Tab bar.</summary>
    Dock,

    /// <summary>
    /// A Tap in progress (the transaction <c>T</c> of blueprint §7.5): its keys are down for a few events only, so
    /// the panic strip does not show it, but it is counted like any holder so no key it shares with another holder is
    /// released under it (INV-1).
    /// </summary>
    Tap,
}
