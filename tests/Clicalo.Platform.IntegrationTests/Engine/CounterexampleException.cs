namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>A property failed; the message names the reduced case to keep as a regression.</summary>
public sealed class CounterexampleException : Exception
{
    public CounterexampleException() { }

    public CounterexampleException(string message)
        : base(message) { }

    public CounterexampleException(string message, Exception innerException)
        : base(message, innerException) { }
}
