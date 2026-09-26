using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Scripting;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Tools.SpikeLab.Tests.Measurement;

public sealed class RepetitionRecorderTests : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);
    private readonly FakeTimeProvider _time = new();
    private readonly List<RepetitionEvidence> _closed = [];
    private readonly RepetitionRecorder _recorder;
    private MeasurementCounters _counters;

    public RepetitionRecorderTests() =>
        _recorder = new RepetitionRecorder(_time, () => _counters, action => action(), _closed.Add);

    [Fact]
    public void An_automatic_repetition_closes_after_the_settle_time_with_what_happened_since_the_previous_one()
    {
        Activate();
        _recorder.Trigger(Partial("bold"), Settle);
        Activate();

        _time.Advance(Settle - TimeSpan.FromMilliseconds(1));
        _closed.ShouldBeEmpty();
        _time.Advance(TimeSpan.FromMilliseconds(1));

        var closed = _closed.ShouldHaveSingleItem();
        closed.Trigger!.Tile.ShouldBe("bold");
        closed.Delta.SurfaceActivations.ShouldBe(
            2,
            "the activation before the trigger belongs to it too"
        );
        _recorder.HasPending.ShouldBeFalse();
    }

    [Fact]
    public void A_new_trigger_closes_the_pending_repetition_first()
    {
        _recorder.Trigger(Partial("bold"), Settle);
        Activate();
        _recorder.Trigger(Partial("copy"), Settle);
        _time.Advance(Settle);

        _closed.Select(evidence => evidence.Trigger!.Tile).ShouldBe(["bold", "copy"]);
        _closed[0].Delta.SurfaceActivations.ShouldBe(1);
        _closed[1].Delta.SurfaceActivations.ShouldBe(0);
    }

    [Fact]
    public void Rebase_drops_the_pending_repetition_and_what_happened_before()
    {
        Activate();
        _recorder.Trigger(Partial("bold"), Settle);

        _recorder.Rebase();
        _time.Advance(Settle);
        _recorder.Trigger(Partial("copy"), Settle);
        _time.Advance(Settle);

        var closed = _closed.ShouldHaveSingleItem();
        closed.Trigger!.Tile.ShouldBe("copy");
        closed.Delta.SurfaceActivations.ShouldBe(0);
    }

    [Fact]
    public void A_manual_mark_takes_what_happened_since_the_previous_repetition()
    {
        _recorder.Trigger(Partial("bold"), Settle);
        Activate();

        var first = _recorder.TakeManual();
        Activate();
        Activate();
        var second = _recorder.TakeManual();

        _closed.ShouldHaveSingleItem().Delta.SurfaceActivations.ShouldBe(1);
        first.SurfaceActivations.ShouldBe(0);
        second.SurfaceActivations.ShouldBe(2);
    }

    public void Dispose() => _recorder.Dispose();

    private static RepetitionEvidence Partial(string tile) =>
        new() { Trigger = new TriggerInfo(StepTrigger.SurfaceTap) { Tile = tile } };

    private void Activate() =>
        _counters = _counters with { SurfaceActivations = _counters.SurfaceActivations + 1 };
}
