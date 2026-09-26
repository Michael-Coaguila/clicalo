using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Reporting;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Tools.SpikeLab.Tests.Measurement;

public sealed class LabEventLogTests
{
    [Fact]
    public void Entries_are_stamped_and_kept_in_order()
    {
        var time = new FakeTimeProvider();
        var log = new LabEventLog(time);

        log.Add("tap", "Toque en Negrita.");
        time.Advance(TimeSpan.FromSeconds(1));
        log.Add("foreground", "Primer plano: notepad.");

        var entries = log.Snapshot();
        entries.Select(entry => entry.Kind).ShouldBe(["tap", "foreground"]);
        (entries[1].At - entries[0].At).ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void The_oldest_entries_are_dropped_and_counted_when_the_log_is_full()
    {
        var log = new LabEventLog(new FakeTimeProvider());

        for (var i = 0; i < ReportContext.MaxEvents + 3; i++)
        {
            log.Add("tap", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var entries = log.Snapshot();
        entries.Length.ShouldBe(ReportContext.MaxEvents);
        entries[0].Detail.ShouldBe("3");
        log.Dropped.ShouldBe(3);
    }
}
