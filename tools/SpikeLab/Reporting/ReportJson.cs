using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// Writes the machine-readable report of a run, format <see cref="Format"/> (documented in
/// docs/testing/spikes/README.md). Enumerations are camelCase strings; times are ISO 8601 with offset; absent values
/// are <c>null</c>. It contains process names, counters, lengths and times only: never a window title or typed text.
/// </summary>
internal static class ReportJson
{
    /// <summary>Identifier and version of the format.</summary>
    public const string Format = "clicalo.spikelab.report/1";

    private static readonly JsonWriterOptions Options = new() { Indented = true };

    /// <summary>The report of <paramref name="run"/> as UTF-8 JSON text.</summary>
    public static string Write(ScriptSnapshot run, ReportContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);
        var buffer = new ArrayBufferWriter<byte>(16 * 1024);
        using (var json = new Utf8JsonWriter(buffer, Options))
        {
            json.WriteStartObject();
            json.WriteString("format", Format);
            json.WriteString("spike", run.Script.Id.ToString());
            json.WriteString("title", run.Script.Title);
            json.WriteString("document", run.Script.Document);
            json.WriteString("startedAt", run.StartedAt);
            json.WriteString("updatedAt", context.UpdatedAt);

            json.WriteStartObject("machine");
            json.WriteString("windows", context.Machine.Windows);
            json.WriteString("architecture", context.Machine.Architecture);
            json.WriteString("labVersion", context.Machine.LabVersion);
            json.WriteEndObject();

            json.WriteStartObject("settings");
            json.WriteBoolean("sendsKeys", context.SendsKeys);
            json.WriteBoolean("voiceNumbers", context.VoiceNumbers);
            json.WriteEndObject();

            json.WriteString("verdict", Name(run.Verdict));
            WriteSummary(json, run);
            if (run.Current is { } current)
            {
                json.WriteString("currentStep", current.Step.Id);
            }
            else
            {
                json.WriteNull("currentStep");
            }

            json.WriteStartArray("components");
            foreach (var component in context.Components)
            {
                json.WriteStartObject();
                json.WriteString("name", component.Name);
                json.WriteString("state", Name(component.State));
                json.WriteString("detail", component.Detail);
                json.WriteEndObject();
            }

            json.WriteEndArray();

            json.WriteStartArray("steps");
            foreach (var progress in run.Steps)
            {
                WriteStep(json, progress);
            }

            json.WriteEndArray();

            json.WriteStartArray("events");
            foreach (var entry in context.Events)
            {
                json.WriteStartObject();
                json.WriteString("at", entry.At);
                json.WriteString("kind", entry.Kind);
                json.WriteString("detail", entry.Detail);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteNumber("droppedEvents", context.DroppedEvents);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    /// <summary>The camelCase name of an enumeration value, as the report writes it.</summary>
    public static string Name<T>(T value)
        where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static void WriteSummary(Utf8JsonWriter json, ScriptSnapshot run)
    {
        json.WriteStartObject("summary");
        json.WriteNumber("steps", run.Steps.Length);
        foreach (var verdict in Enum.GetValues<StepVerdict>())
        {
            json.WriteNumber(Name(verdict), run.Steps.Count(step => step.Verdict == verdict));
        }

        json.WriteEndObject();
    }

    private static void WriteStep(Utf8JsonWriter json, StepProgress progress)
    {
        var step = progress.Step;
        json.WriteStartObject();
        json.WriteString("id", step.Id);
        json.WriteString("title", step.Title);
        json.WriteNumber("required", step.Required);
        json.WriteString("trigger", Name(step.Trigger));
        json.WriteString("surface", Name(step.Surface));
        WriteOptional(json, "pointer", step.Pointer);
        WriteOptional(json, "tile", step.Tile);
        WriteOptional(json, "pattern", step.Pattern);
        WriteOptional(json, "lease", step.Lease);
        WriteOptional(json, "origin", step.Origin);
        json.WriteBoolean("optional", step.Optional);
        json.WriteBoolean("decisive", step.Decisive);
        json.WriteStartArray("checks");
        foreach (var check in Enum.GetValues<EvidenceCheck>())
        {
            if (BitOperations.IsPow2((int)check) && step.Checks.HasFlag(check))
            {
                json.WriteStringValue(Name(check));
            }
        }

        json.WriteEndArray();
        json.WriteString("verdict", Name(progress.Verdict));
        json.WriteNumber("passed", progress.PassedCount);
        json.WriteNumber("failed", progress.FailedCount);
        json.WriteBoolean("confirmed", progress.Confirmed);
        if (progress.StartedAt is { } started)
        {
            json.WriteString("startedAt", started);
        }
        else
        {
            json.WriteNull("startedAt");
        }

        var latency = LatencySummary.Of(progress.Repetitions);
        json.WriteStartObject("latencyMs");
        json.WriteNumber("count", latency.Count);
        json.WriteNumber("p50", Math.Round(latency.P50, 2));
        json.WriteNumber("p95", Math.Round(latency.P95, 2));
        json.WriteNumber("max", Math.Round(latency.Max, 2));
        json.WriteEndObject();

        json.WriteStartArray("repetitions");
        foreach (var repetition in progress.Repetitions)
        {
            WriteRepetition(json, repetition);
        }

        json.WriteEndArray();

        json.WriteStartArray("discarded");
        foreach (var attempt in progress.Discarded)
        {
            json.WriteStartObject();
            json.WriteString("at", attempt.At);
            json.WriteBoolean("confirmed", attempt.Confirmed);
            json.WriteStartArray("repetitions");
            foreach (var repetition in attempt.Repetitions)
            {
                WriteRepetition(json, repetition);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteRepetition(Utf8JsonWriter json, RepetitionRecord repetition)
    {
        json.WriteStartObject();
        json.WriteNumber("index", repetition.Index);
        json.WriteString("at", repetition.At);
        json.WriteString("source", Name(repetition.Source));
        json.WriteBoolean("passed", repetition.Passed);
        json.WriteBoolean("failedByUser", repetition.FailedByUser);
        json.WriteStartArray("problems");
        foreach (var problem in repetition.Problems)
        {
            json.WriteStringValue(problem);
        }

        json.WriteEndArray();
        WriteEvidence(json, repetition.Evidence);
        json.WriteEndObject();
    }

    private static void WriteEvidence(Utf8JsonWriter json, RepetitionEvidence evidence)
    {
        json.WriteStartObject("evidence");
        if (evidence.Trigger is { } trigger)
        {
            json.WriteStartObject("trigger");
            json.WriteString("kind", Name(trigger.Kind));
            WriteOptional(json, "surface", trigger.Surface);
            json.WriteString("group", Name(trigger.Group));
            WriteOptional(json, "tile", trigger.Tile);
            WriteOptional(json, "pattern", trigger.Pattern);
            WriteOptional(json, "pointer", trigger.Pointer);
            json.WriteString("channel", trigger.Channel);
            if (trigger.VoiceNumber is { } number)
            {
                json.WriteNumber("voiceNumber", number);
            }
            else
            {
                json.WriteNull("voiceNumber");
            }

            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("trigger");
        }

        WriteOptional(json, "foregroundProcess", evidence.ForegroundProcess);
        WriteOptional(json, "targetProcess", evidence.TargetProcess);
        if (evidence.TargetInFront is { } inFront)
        {
            json.WriteBoolean("targetInFront", inFront);
        }
        else
        {
            json.WriteNull("targetInFront");
        }

        WriteOptional(json, "latencyMs", evidence.LatencyMs);

        var counters = evidence.Delta;
        json.WriteStartObject("counters");
        json.WriteNumber("foregroundChanges", counters.ForegroundChanges);
        json.WriteNumber("ownForegroundChanges", counters.OwnForegroundChanges);
        json.WriteNumber("surfaceActivations", counters.SurfaceActivations);
        json.WriteNumber("violations", counters.Violations);
        json.WriteNumber("probeF24", counters.ProbeF24);
        json.WriteNumber("probeChars", counters.ProbeChars);
        json.WriteNumber("probeMenus", counters.ProbeMenus);
        json.WriteNumber("probeModifierKeys", counters.ProbeModifierKeys);
        json.WriteNumber("rightsChords", counters.RightsChords);
        json.WriteNumber("refusedInjections", counters.RefusedInjections);
        json.WriteEndObject();

        if (evidence.Lease is { } lease)
        {
            json.WriteStartObject("lease");
            json.WriteString("kind", Name(lease.Kind));
            json.WriteString("origin", Name(lease.Origin));
            json.WriteBoolean("granted", lease.Granted);
            WriteOptional(json, "denial", lease.Denial);
            WriteOptional(json, "unavailable", lease.Unavailable);
            json.WriteNumber("ladderStep", lease.LadderStep);
            json.WriteNumber("acquireMs", Math.Round(lease.AcquireMs, 2));
            WriteOptional(json, "restore", lease.Restore);
            json.WriteNumber("restoreMs", Math.Round(lease.RestoreMs, 2));
            WriteOptional(json, "previousProcess", lease.PreviousProcess);
            json.WriteBoolean("previousWasProbe", lease.PreviousWasProbe);
            json.WriteBoolean("foregroundReturned", lease.ForegroundReturned);
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("lease");
        }

        WriteOptional(json, "restoredWithinMs", evidence.RestoredWithinMs);
        if (evidence.NoActivateStyleKept is { } kept)
        {
            json.WriteBoolean("noActivateStyleKept", kept);
        }
        else
        {
            json.WriteNull("noActivateStyleKept");
        }

        json.WriteStartObject("fields");
        json.WriteNumber("count", evidence.FieldCount);
        json.WriteNumber("withText", evidence.FieldsWithText);
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null)
        {
            json.WriteNull(name);
        }
        else
        {
            json.WriteString(name, value);
        }
    }

    private static void WriteOptional(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, Math.Round(number, 2));
        }
        else
        {
            json.WriteNull(name);
        }
    }

    private static void WriteOptional<T>(Utf8JsonWriter json, string name, T? value)
        where T : struct, Enum
    {
        if (value is { } present)
        {
            json.WriteString(name, Name(present));
        }
        else
        {
            json.WriteNull(name);
        }
    }
}
