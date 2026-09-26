namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// The injector refused to send a batch because a safety precondition did not hold (nothing was injected), or
/// detected after sending that the precondition broke during the call.
/// </summary>
public sealed class InjectionRefusedException : Exception
{
    public InjectionRefusedException() { }

    public InjectionRefusedException(string message)
        : base(message) { }

    public InjectionRefusedException(string message, Exception innerException)
        : base(message, innerException) { }
}
