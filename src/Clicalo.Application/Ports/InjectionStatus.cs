namespace Clicalo.Application.Ports;

/// <summary>What happened to a batch sent through <see cref="IInputInjector"/>.</summary>
public enum InjectionStatus
{
    /// <summary>Every event was sent.</summary>
    Sent,

    /// <summary><c>SendInput</c> sent fewer events than asked.</summary>
    Failed,

    /// <summary>The secure desktop is in front (locked session): releases are kept and sent again on unlock (INV-3).</summary>
    Blocked,
}
