using Clicalo.Application.Ports;

namespace Clicalo.Infrastructure.Updates;

/// <summary>An expected failure of the installer, already classified for the card (ACT-001).</summary>
internal sealed class UpdateFailedException : Exception
{
    /// <summary>Creates the failure.</summary>
    /// <param name="error">Why it failed.</param>
    /// <param name="inner">The original exception, never shown nor logged with its message.</param>
    public UpdateFailedException(UpdateError error, Exception? inner = null)
        : base("update." + error.ToString().ToLowerInvariant(), inner) => Error = error;

    /// <summary>Creates an interrupted failure.</summary>
    public UpdateFailedException()
        : this(UpdateError.Interrupted) { }

    /// <summary>Creates an interrupted failure.</summary>
    /// <param name="message">Ignored: the message is the stable code.</param>
    public UpdateFailedException(string message)
        : this(UpdateError.Interrupted) => _ = message;

    /// <summary>Creates an interrupted failure.</summary>
    /// <param name="message">Ignored: the message is the stable code.</param>
    /// <param name="inner">The original exception.</param>
    public UpdateFailedException(string message, Exception inner)
        : this(UpdateError.Interrupted, inner) => _ = message;

    /// <summary>Why it failed.</summary>
    public UpdateError Error { get; }
}
