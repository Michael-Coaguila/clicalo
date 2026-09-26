namespace Clicalo.Build;

/// <summary>
/// An expected failure of a step (a compile error, a failed test, an unformatted file). It stops the verb,
/// Bullseye shows only its message, and <see cref="Details"/> becomes <c>artifacts/cl/last-error.md</c>.
/// </summary>
internal sealed class StepFailedException : Exception
{
    public StepFailedException()
        : this(new FailureDetails { Summary = Messages.UnexpectedFailed }) { }

    public StepFailedException(string message)
        : this(new FailureDetails { Summary = message }) { }

    public StepFailedException(string message, Exception innerException)
        : base(message, innerException) => Details = new FailureDetails { Summary = message };

    public StepFailedException(FailureDetails details)
        : base(details.Summary) => Details = details;

    /// <summary>The report of the failure.</summary>
    public FailureDetails Details { get; }
}
