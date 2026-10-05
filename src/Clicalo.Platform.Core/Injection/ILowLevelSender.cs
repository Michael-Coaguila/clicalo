namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// Sends events to the system. <see cref="LowLevelInjector"/> is the real one (the only <c>SendInput</c> of the
/// product); the tests give their own, so every caller is tested without touching the keyboard.
/// </summary>
public interface ILowLevelSender
{
    /// <summary>Sends the events in one call.</summary>
    /// <param name="inputs">The events, in order.</param>
    SendResult Send(ReadOnlySpan<LowLevelInput> inputs);
}
