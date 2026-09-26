namespace Clicalo.Application.Confirmation;

/// <summary>
/// Proof of the second tap of a destructive operation (REG-04). Only <see cref="TwoStepConfirm"/> creates it (CLC0010),
/// and destructive commands and use cases can only be dispatched with one. The analyzer binds to it by metadata name
/// (<c>docs/guides/analyzers.md</c>): do not rename or move it.
/// </summary>
public sealed class ConfirmationToken
{
    internal ConfirmationToken(ConfirmationSubject subject, DateTimeOffset confirmedAt)
    {
        Subject = subject;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>What was confirmed.</summary>
    public ConfirmationSubject Subject { get; }

    /// <summary>When the second tap happened.</summary>
    public DateTimeOffset ConfirmedAt { get; }
}
