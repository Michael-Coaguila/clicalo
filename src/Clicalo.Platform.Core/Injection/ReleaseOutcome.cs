using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.Injection;

/// <summary>What one <see cref="PressedInputRelease.ReleaseOnce"/> did.</summary>
/// <param name="Readable">Whether the key state could be read (<see cref="IKeyStateReader.CanRead"/>).</param>
/// <param name="Events">How many events the release had (0 when nothing was down).</param>
/// <param name="Send">What <c>SendInput</c> returned; default when nothing was sent.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ReleaseOutcome(bool Readable, int Events, SendResult Send)
{
    /// <summary>Whether everything that was down went up: readable, and every event accepted.</summary>
    public bool Completed => Readable && Send.Sent >= Events;
}
