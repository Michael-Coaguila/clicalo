using System.Text.RegularExpressions;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// timings.json defines every time and threshold once (NFR-020, blueprint §13): the NFR-020 table, unique names,
/// explicit units and traceable sources.
/// </summary>
public sealed partial class TimingsTests
{
    private static TimingsCatalog Timings => TimingsCatalog.Shared;

    /// <summary>The NFR-020 table of the requirements catalog, row by row.</summary>
    public static TheoryData<string, string> Nfr020() =>
        new()
        {
            { "Touch.LongPress", "600ms" },
            { "Confirmation.ExecuteConfirmWindow", "3s" },
            { "Confirmation.DestructiveConfirmWindow", "3.5s" },
            { "Notices.NoticeDuration", "3.2s" },
            { "Notices.UndoNoticeDuration", "6s" },
            { "Notices.ExecutionFlash", "240ms" },
            { "TestMode.TestMarkDuration", "700ms" },
            { "TestMode.TestModeDuration", "30s" },
            { "Dock.CollapseDelay", "900ms" },
            { "Dimming.DimDelay", "2.5s" },
            { "Dimming.DimTransition", "350ms" },
            { "Touch.SwipeMinDistancePx", "60" },
            { "Touch.PostSwipeLock", "300ms" },
            { "Touch.DragMinDistancePx", "6" },
            { "KeySafety.AutoReleaseChoices", "30s,60s,120s" },
            { "Macro.MacroWaitRange", "100ms,10s,100ms" },
            { "Mouse.ScrollRepeatSlow", "60ms" },
            { "Mouse.ScrollRepeatNormal", "40ms" },
            { "Mouse.ScrollRepeatFast", "25ms" },
            { "Injection.ClipboardRestoreDelay", "500ms" },
            { "Ai.AiRequestTimeout", "15s" },
            { "TryNow.TryNowSendDelay", "0.9s" },
            { "TryNow.TryNowReturnDelay", "2.6s" },
            { "Backups.AutoBackupDelay", "30s" },
            { "Updates.UpdateIdleRequired", "5min" },
            { "Backups.PreviousVersionRetention", "7d" },
        };

    [Theory]
    [MemberData(nameof(Nfr020))]
    [Trait("Req", "NFR-020")]
    public void Every_constant_of_NFR_020_is_defined_with_its_value(string path, string expected)
    {
        var entry = Timings.Entries.Single(e => Ordinal.Is(e.Path, path));

        var actual = entry.Kind switch
        {
            "durations" => string.Join(
                ",",
                entry.Value.AsArray().Select(v => v!.GetValue<string>())
            ),
            "durationRange" => string.Join(
                ",",
                new[] { "min", "max", "step" }.Select(p => entry.Value[p]!.GetValue<string>())
            ),
            "px" or "count" => entry
                .Value.GetValue<int>()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => entry.Value.GetValue<string>(),
        };

        actual.ShouldBe(expected);
        entry.Requirements.ShouldContain(r => Ordinal.Is(r, "NFR-020"));
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Documented_architecture_thresholds_have_their_blueprint_values()
    {
        Window("Guardian.RestartLoop").ShouldBe((3, TimeSpan.FromMinutes(10)));
        Window("App.CrashLoop").ShouldBe((3, TimeSpan.FromMinutes(10)));
        Duration("Persistence.DocumentSaveDebounce").ShouldBe(TimeSpan.FromMilliseconds(500));
        Duration("Persistence.DocumentSaveMaxLatency").ShouldBe(TimeSpan.FromSeconds(2));
        Duration("Persistence.UsageSaveDebounce").ShouldBe(TimeSpan.FromSeconds(30));
        Duration("Persistence.UsageSaveMaxLatency").ShouldBe(TimeSpan.FromMinutes(5));
        Duration("Injection.InterEventDelay").ShouldBe(TimeSpan.FromMilliseconds(20));
        Timings
            .Entries.Single(e => Ordinal.Is(e.Path, "Backups.AutoBackupRetention"))
            .Value.GetValue<int>()
            .ShouldBe(12);
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void No_threshold_is_defined_twice()
    {
        // A name identifies one threshold in the whole file: the same name in two groups would let two values
        // drift apart (as the drag threshold did in the prototype, DIS-40).
        Timings.Entries.Select(e => e.Name).ShouldBeUnique(StringComparer.OrdinalIgnoreCase);
        Timings
            .Entries.Select(e => e.Group)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(
                Timings
                    .Entries.Select(e => e.Group)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
            );
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Names_carry_the_unit_of_unitless_values()
    {
        foreach (var entry in Timings.Entries)
        {
            switch (entry.Kind)
            {
                case "px":
                    entry.Name.ShouldEndWith("Px", Case.Sensitive, entry.Path);
                    break;
                case "bytes":
                    entry.Name.ShouldEndWith("Bytes", Case.Sensitive, entry.Path);
                    break;
                default:
                    entry.Name.ShouldNotEndWith("Px", entry.Path, Case.Sensitive);
                    break;
            }
        }
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Every_value_parses_with_its_unit()
    {
        foreach (var entry in Timings.Entries)
        {
            switch (entry.Kind)
            {
                case "duration":
                    ParseDuration(entry.Value.GetValue<string>())
                        .ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero, entry.Path);
                    break;
                case "durations":
                    var values = entry
                        .Value.AsArray()
                        .Select(v => ParseDuration(v!.GetValue<string>()))
                        .ToList();
                    values.ShouldBe(values.Order().ToList(), entry.Path + " must be ascending");
                    break;
                case "bytes":
                    TimingsCatalog
                        .Bytes(entry.Value.GetValue<string>())
                        .ShouldBeGreaterThan(0, entry.Path);
                    break;
                case "countWindow":
                    ParseDuration(entry.Value["window"]!.GetValue<string>())
                        .ShouldBeGreaterThan(TimeSpan.Zero, entry.Path);
                    break;
                case "durationRange":
                    var min = ParseDuration(entry.Value["min"]!.GetValue<string>());
                    var max = ParseDuration(entry.Value["max"]!.GetValue<string>());
                    var step = ParseDuration(entry.Value["step"]!.GetValue<string>());
                    min.ShouldBeLessThanOrEqualTo(max, entry.Path);
                    (min.Ticks % step.Ticks).ShouldBe(0, entry.Path);
                    (max.Ticks % step.Ticks).ShouldBe(0, entry.Path);
                    break;
            }
        }
    }

    [Fact]
    public void Every_cited_requirement_exists_in_the_requirements_catalog()
    {
        var catalog = File.ReadAllText(RepoPaths.Combine("docs", "requirements", "catalog.md"));
        var known = RequirementId()
            .Matches(catalog)
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = Timings.Entries.SelectMany(e =>
            e.Requirements.Where(r => !known.Contains(r)).Select(r => e.Path + " → " + r)
        );

        unknown.ShouldBeEmpty();
        Timings.Entries.ShouldAllBe(e => e.Requirements.Count > 0 || e.Node["source"] != null);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void Mouse_scroll_speeds_reference_named_repeat_intervals_from_slow_to_fast()
    {
        var mouse = CatalogFiles.LoadObject(CatalogFiles.Catalog("mouse.json"));
        var intervals = mouse["scrollSpeeds"]!
            .AsArray()
            .Select(s => s!["repeatTiming"]!.GetValue<string>())
            .Select(path => Timings.Entries.SingleOrDefault(e => Ordinal.Is(e.Path, path)))
            .ToList();

        intervals.ShouldAllBe(e => e != null && e.Kind == "duration");
        var values = intervals.Select(e => ParseDuration(e!.Value.GetValue<string>())).ToList();
        values.ShouldBe(values.OrderDescending().ToList(), "slow repeats less often than fast");
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    public void Touch_presets_have_the_documented_values_and_mild_tremor_is_the_default()
    {
        var presets = CatalogFiles.LoadObject(CatalogFiles.Catalog("touch-presets.json"));
        var values = presets["presets"]!
            .AsArray()
            .ToDictionary(
                p => p!["id"]!.GetValue<string>(),
                p =>
                    (
                        ParseDuration(p!["debounce"]!.GetValue<string>()).TotalMilliseconds,
                        p["hitSlopPx"]!.GetValue<int>(),
                        p["cancelMovePx"]!.GetValue<int>(),
                        ParseDuration(p["minContact"]!.GetValue<string>()).TotalMilliseconds
                    ),
                StringComparer.Ordinal
            );

        values["standard"].ShouldBe((150d, 8, 45, 0d));
        values["mild-tremor"].ShouldBe((300d, 14, 35, 0d));
        values["strong-tremor"].ShouldBe((600d, 24, 28, 80d));
        presets["default"]!.GetValue<string>().ShouldBe("mild-tremor");
    }

    private static TimeSpan ParseDuration(string text) => TimingsCatalog.Duration(text);

    private static TimeSpan Duration(string path) =>
        ParseDuration(
            Timings.Entries.Single(e => Ordinal.Is(e.Path, path)).Value.GetValue<string>()
        );

    private static (int Count, TimeSpan Window) Window(string path)
    {
        var value = Timings.Entries.Single(e => Ordinal.Is(e.Path, path)).Value;
        return (
            value["count"]!.GetValue<int>(),
            ParseDuration(value["window"]!.GetValue<string>())
        );
    }

    [GeneratedRegex(
        @"\*\*(?<id>[A-Z]{3}-\d{2,3})\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex RequirementId();
}
