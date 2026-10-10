using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Confirmation;

/// <summary>
/// The two-tap confirmation of destructive operations (REG-04): the first tap arms for
/// <c>Timings.Confirmation.DestructiveConfirmWindow</c> (3.5 s), a second tap on the same subject in time returns the
/// only kind of <see cref="ConfirmationToken"/> there is. Arming another subject replaces the armed one. The analyzer
/// binds to it by metadata name (<c>docs/guides/analyzers.md</c>): do not rename or move it.
/// </summary>
/// <remarks>
/// The window is half open: a second tap exactly when it ends arms again instead of confirming. A confirmation
/// disarms, so a third tap arms again.
/// </remarks>
public sealed class TwoStepConfirm
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly Func<int>? _multiplier;
    private ConfirmationSubject? _armed;
    private DateTimeOffset _until;

    /// <summary>Creates a confirmation.</summary>
    /// <param name="time">Clock of the confirmation window.</param>
    public TwoStepConfirm(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Creates a confirmation whose window follows the adjustable times (ACC-006).</summary>
    /// <param name="time">Clock of the confirmation window.</param>
    /// <param name="multiplier">
    /// The time multiplier of the settings (×1, ×2 or ×3), read when a first tap arms: the window lasts that many times
    /// <c>Timings.Confirmation.DestructiveConfirmWindow</c> (<see cref="InteractionTime"/>).
    /// </param>
    public TwoStepConfirm(TimeProvider time, Func<int> multiplier)
        : this(time)
    {
        ArgumentNullException.ThrowIfNull(multiplier);
        _multiplier = multiplier;
    }

    /// <summary>The armed subject, or <see langword="null"/> when nothing is armed or the window passed.</summary>
    public ConfirmationSubject? ArmedSubject
    {
        get
        {
            lock (_gate)
            {
                return _armed is { } armed && _time.GetUtcNow() < _until ? armed : null;
            }
        }
    }

    /// <summary>A tap on the destructive control of <paramref name="subject"/>.</summary>
    /// <param name="subject">What the control would destroy.</param>
    public TwoStepResult Tap(ConfirmationSubject subject)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_armed is { } armed && armed == subject && now < _until)
            {
                _armed = null;
                return new TwoStepResult.Confirmed(new ConfirmationToken(subject, now));
            }

            _armed = subject;
            _until =
                now
                + InteractionTime.Scale(
                    Timings.Confirmation.DestructiveConfirmWindow,
                    _multiplier?.Invoke() ?? 1
                );
            return new TwoStepResult.Armed(subject, _until);
        }
    }

    /// <summary>Disarms (another control was used, or the surface closed).</summary>
    public void Disarm()
    {
        lock (_gate)
        {
            _armed = null;
        }
    }
}
