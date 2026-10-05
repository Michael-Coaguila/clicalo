using Clicalo.Application.Confirmation;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Confirmation;

/// <summary>
/// Nothing destructive with one tap (REG-04): the first tap arms for <c>Timings.Confirmation.DestructiveConfirmWindow</c>
/// (3.5 s, «[delConfirm]»), a second tap on the same subject in time confirms, anything else re-arms or disarms.
/// </summary>
[Trait("Req", "REG-04")]
public sealed class TwoStepConfirmTests
{
    private static readonly ConfirmationSubject DeleteBold = new("DeleteShortcut", "bold");
    private static readonly ConfirmationSubject DeleteSave = new("DeleteShortcut", "save");

    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public void The_first_tap_arms_for_three_and_a_half_seconds()
    {
        var confirm = new TwoStepConfirm(_time);

        var armed = confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>();

        Timings.Confirmation.DestructiveConfirmWindow.ShouldBe(TimeSpan.FromSeconds(3.5));
        armed.Subject.ShouldBe(DeleteBold);
        armed.Until.ShouldBe(_time.After(Timings.Confirmation.DestructiveConfirmWindow));
        confirm.ArmedSubject.ShouldBe(DeleteBold);
    }

    [Fact]
    public void A_second_tap_in_time_confirms_and_disarms()
    {
        var confirm = new TwoStepConfirm(_time);
        var until = confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>().Until;
        _time.AdvanceToJustBefore(until);

        var token = confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Confirmed>().Token;

        token.Subject.ShouldBe(DeleteBold);
        token.ConfirmedAt.ShouldBe(_time.GetUtcNow());
        confirm.ArmedSubject.ShouldBeNull();
        confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>();
    }

    [Fact]
    public void A_second_tap_when_the_window_ends_only_arms_again()
    {
        var confirm = new TwoStepConfirm(_time);
        var until = confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>().Until;
        _time.AdvanceToJustBefore(until);
        confirm.ArmedSubject.ShouldBe(DeleteBold);
        _time.AdvanceTo(until);

        confirm.ArmedSubject.ShouldBeNull();
        confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>();
    }

    [Fact]
    public void Arming_another_subject_replaces_the_armed_one()
    {
        var confirm = new TwoStepConfirm(_time);
        confirm.Tap(DeleteBold);

        confirm.Tap(DeleteSave).ShouldBeOfType<TwoStepResult.Armed>();

        confirm.ArmedSubject.ShouldBe(DeleteSave);
        confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>();
    }

    [Fact]
    public void Disarming_forgets_the_first_tap()
    {
        var confirm = new TwoStepConfirm(_time);
        confirm.Tap(DeleteBold);

        confirm.Disarm();

        confirm.ArmedSubject.ShouldBeNull();
        confirm.Tap(DeleteBold).ShouldBeOfType<TwoStepResult.Armed>();
    }
}
