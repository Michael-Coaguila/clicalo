namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// Sends events to the system. <see cref="LowLevelInjector"/> is the real one (the only <c>SendInput</c> of the
/// product); the tests' physical state injector is the other, so the gate is tested with its real code (§7.10).
/// </summary>
public interface ILowLevelSender
{
    /// <summary>Sends the events in one call.</summary>
    /// <param name="inputs">The events, in order.</param>
    SendResult Send(ReadOnlySpan<LowLevelInput> inputs);
}
