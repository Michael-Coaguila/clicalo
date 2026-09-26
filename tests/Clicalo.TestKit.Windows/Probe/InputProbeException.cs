namespace Clicalo.TestKit.Windows.Probe;

/// <summary>The probe could not be started, did not answer in time, broke the protocol or exited early.</summary>
public sealed class InputProbeException : Exception
{
    public InputProbeException() { }

    public InputProbeException(string message)
        : base(message) { }

    public InputProbeException(string message, Exception innerException)
        : base(message, innerException) { }
}
