using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Application.Confirmation;

/// <summary>
/// The two-tap confirmation of destructive operations (REG-04): the first tap arms for
/// <c>Timings.Confirmation.DestructiveConfirmWindow</c> (3.5 s), a second tap on the same subject in time returns the
/// only kind of <see cref="ConfirmationToken"/> there is. Arming another subject replaces the armed one. The analyzer
/// binds to it by metadata name (<c>docs/guides/analyzers.md</c>): do not rename or move it.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class TwoStepConfirm
{
    /// <summary>Creates a confirmation.</summary>
    /// <param name="time">Clock of the confirmation window.</param>
    public TwoStepConfirm(TimeProvider time) => throw new NotImplementedException();

    /// <summary>The armed subject, or <see langword="null"/> when nothing is armed or the window passed.</summary>
    public ConfirmationSubject? ArmedSubject => throw new NotImplementedException();

    /// <summary>A tap on the destructive control of <paramref name="subject"/>.</summary>
    /// <param name="subject">What the control would destroy.</param>
    public TwoStepResult Tap(ConfirmationSubject subject) => throw new NotImplementedException();

    /// <summary>Disarms (another control was used, or the surface closed).</summary>
    public void Disarm() => throw new NotImplementedException();
}
