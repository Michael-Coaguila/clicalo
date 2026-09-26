using System.Text.Json;
using Clicalo.Tools.SpikeLab.Reporting;

namespace Clicalo.Tools.SpikeLab.Tests.Reporting;

/// <summary>The JSON report keeps the documented format (docs/testing/spikes/README.md).</summary>
public sealed class ReportJsonTests
{
    private static readonly JsonElement Root = JsonDocument
        .Parse(ReportJson.Write(ReportFixture.Run(), ReportFixture.Context()))
        .RootElement;

    [Fact]
    public void The_header_names_the_format_the_spike_and_the_machine()
    {
        Root.GetProperty("format").GetString().ShouldBe("clicalo.spikelab.report/1");
        Root.GetProperty("spike").GetString().ShouldBe("S1");
        Root.GetProperty("document").GetString().ShouldBe("docs/testing/spikes/S1.md");
        Root.GetProperty("startedAt").GetDateTimeOffset().ShouldBe(ReportFixture.Start);
        Root.GetProperty("machine")
            .GetProperty("labVersion")
            .GetString()
            .ShouldBe("2.0.0-dev+abc123");
        Root.GetProperty("settings").GetProperty("voiceNumbers").GetBoolean().ShouldBeTrue();
        Root.GetProperty("verdict").GetString().ShouldBe("failed");
        Root.GetProperty("currentStep").GetString().ShouldBe("5");
    }

    [Fact]
    public void The_machine_has_the_windows_revision_the_input_hardware_and_the_monitors()
    {
        var machine = Root.GetProperty("machine");

        machine.GetProperty("windows").GetString().ShouldBe("10.0.26200.6584 (25H2)");
        machine.GetProperty("windowsBuild").GetString().ShouldBe("10.0.26200.6584");
        machine.GetProperty("windowsDisplayVersion").GetString().ShouldBe("25H2");
        var input = machine.GetProperty("input");
        input.GetProperty("touch").GetBoolean().ShouldBeTrue();
        input.GetProperty("maxTouches").GetInt32().ShouldBe(10);
        input.GetProperty("pen").GetBoolean().ShouldBeTrue();
        input.GetProperty("mouse").GetBoolean().ShouldBeTrue();
        var monitors = machine.GetProperty("monitors").EnumerateArray().ToArray();
        monitors.Length.ShouldBe(2);
        monitors[0].GetProperty("primary").GetBoolean().ShouldBeTrue();
        monitors[0].GetProperty("width").GetInt32().ShouldBe(2400);
        monitors[0].GetProperty("workHeight").GetInt32().ShouldBe(1516);
        monitors[0].GetProperty("scalePercent").GetInt32().ShouldBe(175);
        monitors[1].GetProperty("scalePercent").GetInt32().ShouldBe(100);
    }

    [Fact]
    public void The_summary_counts_the_steps_by_verdict()
    {
        var summary = Root.GetProperty("summary");

        summary.GetProperty("steps").GetInt32().ShouldBe(3);
        summary.GetProperty("passed").GetInt32().ShouldBe(1);
        summary.GetProperty("failed").GetInt32().ShouldBe(1);
        summary.GetProperty("inProgress").GetInt32().ShouldBe(1);
        summary.GetProperty("notApplicable").GetInt32().ShouldBe(0);
    }

    [Fact]
    public void Components_keep_their_state()
    {
        var components = Root.GetProperty("components").EnumerateArray().ToArray();

        components
            .Select(component => component.GetProperty("state").GetString())
            .ShouldBe(["ready", "pending"]);
    }

    [Fact]
    public void Steps_carry_their_rule_verdict_latency_repetitions_and_discarded_attempts()
    {
        var steps = Root.GetProperty("steps").EnumerateArray().ToArray();
        var first = steps[0];

        first.GetProperty("trigger").GetString().ShouldBe("surfaceTap");
        first.GetProperty("surface").GetString().ShouldBe("panel");
        first
            .GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetString())
            .ShouldBe([
                "noSurfaceActivation",
                "noOwnForeground",
                "noViolation",
                "targetStillInFront",
            ]);
        first.GetProperty("verdict").GetString().ShouldBe("passed");
        first.GetProperty("confirmed").GetBoolean().ShouldBeTrue();
        first.GetProperty("latencyMs").GetProperty("max").GetDouble().ShouldBe(30);

        var second = steps[1];
        second.GetProperty("verdict").GetString().ShouldBe("failed");
        second.GetProperty("discarded").GetArrayLength().ShouldBe(1);
        var failed = second.GetProperty("repetitions")[0];
        failed.GetProperty("passed").GetBoolean().ShouldBeFalse();
        failed.GetProperty("problems").GetArrayLength().ShouldBe(2);
        failed
            .GetProperty("evidence")
            .GetProperty("counters")
            .GetProperty("surfaceActivations")
            .GetInt64()
            .ShouldBe(1);
    }

    [Fact]
    public void A_lease_repetition_records_ladder_step_times_and_restoration()
    {
        var lease = Root.GetProperty("steps")[2]
            .GetProperty("repetitions")[0]
            .GetProperty("evidence")
            .GetProperty("lease");

        lease.GetProperty("kind").GetString().ShouldBe("textInput");
        lease.GetProperty("origin").GetString().ShouldBe("uiaInvoke");
        lease.GetProperty("ladderStep").GetInt32().ShouldBe(2);
        lease.GetProperty("acquireMs").GetDouble().ShouldBe(41.23);
        lease.GetProperty("restore").GetString().ShouldBe("restored");
        lease.GetProperty("denial").ValueKind.ShouldBe(JsonValueKind.Null);
        lease.GetProperty("previousWasProbe").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Evidence_has_exactly_the_documented_fields_and_no_title_or_text()
    {
        var evidence = Root.GetProperty("steps")[0]
            .GetProperty("repetitions")[0]
            .GetProperty("evidence");

        evidence
            .EnumerateObject()
            .Select(property => property.Name)
            .ShouldBe([
                "trigger",
                "foregroundProcess",
                "targetProcess",
                "targetInFront",
                "latencyMs",
                "counters",
                "lease",
                "restoredWithinMs",
                "noActivateStyleKept",
                "fields",
            ]);
        evidence.GetProperty("foregroundProcess").GetString().ShouldBe("notepad");
        evidence
            .GetProperty("fields")
            .EnumerateObject()
            .Select(field => field.Name)
            .ShouldBe(["count", "withText"]);
    }

    [Fact]
    public void The_timeline_is_kept_with_the_dropped_count()
    {
        Root.GetProperty("events").GetArrayLength().ShouldBe(1);
        Root.GetProperty("events")[0].GetProperty("kind").GetString().ShouldBe("foreground");
        Root.GetProperty("droppedEvents").GetInt32().ShouldBe(0);
    }
}
