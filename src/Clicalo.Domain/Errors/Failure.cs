using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Errors;

/// <summary>
/// An expected failure (blueprint §6.1): a stable code for logs and tests, the text shown to the user and how the UI
/// reacts. Exceptions are only for defects; everything the user can cause or recover from travels as an
/// <see cref="Failure"/> inside a <see cref="Result{T}"/> (the blueprint calls it <c>Error</c>; renamed because that is a reserved word in Visual Basic, CA1716).
/// </summary>
/// <param name="Code">Stable, dotted code (<c>persist.io.locked</c>, <c>library.process.bound</c>).</param>
/// <param name="Message">Text for the user, localized when painted.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Recovery">What the UI offers to recover.</param>
/// <param name="Announcement">How a screen reader announces it.</param>
public sealed record Failure(
    string Code,
    Message Message,
    FailureSeverity Severity,
    FailureRecovery Recovery,
    FailureAnnouncement Announcement
);
