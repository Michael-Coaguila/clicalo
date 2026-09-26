using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>
/// A broken catalog is a compile error at the exact line and column of the JSON file (blueprint §1.1, idea 4),
/// with one diagnostic id per kind of mistake (CLCC001–CLCC010).
/// </summary>
public sealed class CatalogGeneratorDiagnosticsTests
{
    /// <summary>
    /// Case → catalog to break, text to replace, replacement, expected id, and the text whose first character
    /// (plus an offset) is where the diagnostic must point.
    /// </summary>
    private static readonly Dictionary<string, BrokenCatalog> Cases = new(StringComparer.Ordinal)
    {
        ["invalid-json"] = new(
            "timings.json",
            "\"source\": \"docs/02\" }",
            "\"source\": \"docs/02\", }",
            "CLCC001",
            "\"docs/02\", }",
            11
        ),
        ["duplicate-key-id"] = new(
            "keys.json",
            "\"id\": \"lctrl\"",
            "\"id\": \"ctrl\"",
            "CLCC002",
            "\"id\": \"ctrl\", \"codeName\": \"LeftCtrl\"",
            6
        ),
        ["duplicate-code-name"] = new(
            "keys.json",
            "\"codeName\": \"NTilde\"",
            "\"codeName\": \"A\"",
            "CLCC002",
            "\"codeName\": \"A\", \"group\": \"letters\", \"label\": { \"es\": \"Ñ\"",
            12
        ),
        ["upper-case-key-id"] = new(
            "keys.json",
            "\"id\": \"a\"",
            "\"id\": \"A\"",
            "CLCC003",
            "\"id\": \"A\"",
            6
        ),
        ["upper-case-character"] = new(
            "keys.json",
            "\"id\": \"char:ñ\"",
            "\"id\": \"char:Ñ\"",
            "CLCC003",
            "\"char:Ñ\"",
            0
        ),
        ["decomposed-character"] = new(
            "keys.json",
            "\"id\": \"char:ñ\"",
            "\"id\": \"char:ñ\"",
            "CLCC003",
            "\"char:ñ\"",
            0
        ),
        ["ascii-letter-as-character"] = new(
            "keys.json",
            "\"id\": \"a\"",
            "\"id\": \"char:a\"",
            "CLCC003",
            "\"char:a\"",
            0
        ),
        ["empty-dotted-segment"] = new(
            "keys.json",
            "\"id\": \"a\"",
            "\"id\": \"num..add\"",
            "CLCC003",
            "\"num..add\"",
            0
        ),
        ["negative-duration"] = new(
            "timings.json",
            "\"600ms\"",
            "\"-600ms\"",
            "CLCC004",
            "\"-600ms\"",
            0
        ),
        ["negative-pixels"] = new("timings.json", "\"px\": 60", "\"px\": -60", "CLCC004", "-60", 0),
        ["negative-preset-distance"] = new(
            "touch-presets.json",
            "\"hitSlopPx\": 8",
            "\"hitSlopPx\": -8",
            "CLCC004",
            "-8",
            0
        ),
        ["duration-as-number"] = new(
            "timings.json",
            "\"duration\": \"600ms\"",
            "\"duration\": 600",
            "CLCC005",
            "600,",
            0
        ),
        ["duration-without-unit"] = new(
            "timings.json",
            "\"duration\": \"600ms\"",
            "\"duration\": \"600\"",
            "CLCC005",
            "\"600\"",
            0
        ),
        ["window-without-unit"] = new(
            "timings.json",
            "\"window\": \"10min\"",
            "\"window\": \"10\"",
            "CLCC005",
            "\"10\"",
            0
        ),
        ["preset-without-unit"] = new(
            "touch-presets.json",
            "\"debounce\": \"150ms\"",
            "\"debounce\": \"150\"",
            "CLCC005",
            "\"150\"",
            0
        ),
        ["bytes-without-unit"] = new(
            "timings.json",
            "\"16KiB\"",
            "\"16384\"",
            "CLCC005",
            "\"16384\"",
            0
        ),
        ["duration-with-unknown-unit"] = new(
            "timings.json",
            "\"duration\": \"600ms\"",
            "\"duration\": \"600sec\"",
            "CLCC005",
            "\"600sec\"",
            0
        ),
        ["bytes-with-unknown-unit"] = new(
            "timings.json",
            "\"16KiB\"",
            "\"16KB\"",
            "CLCC005",
            "\"16KB\"",
            0
        ),
        ["duration-with-space-before-unit"] = new(
            "timings.json",
            "\"duration\": \"600ms\"",
            "\"duration\": \"600 ms\"",
            "CLCC009",
            "\"600 ms\"",
            0
        ),
        ["duration-too-large"] = new(
            "timings.json",
            "\"duration\": \"600ms\"",
            "\"duration\": \"99999999999999999999999999999d\"",
            "CLCC009",
            "\"99999999999999999999999999999d\"",
            0
        ),
        ["key-without-mapping"] = new(
            "keys.win32.json",
            "\"a\": { \"vk\": \"0x41\", \"vkName\": \"A\", \"scan\": \"0x1E\", \"extended\": false },",
            string.Empty,
            "CLCC006",
            "\"id\": \"a\"",
            6,
            ReportedIn: "keys.json"
        ),
        ["mapping-of-unknown-key"] = new(
            "keys.win32.json",
            "\"char:ñ\": { \"resolve\": \"character\" }",
            "\"char:ñ\": { \"resolve\": \"character\" }, \"zz\": { \"resolve\": \"character\" }",
            "CLCC007",
            "\"zz\": {",
            6
        ),
        ["code-name-hides-object-member"] = new(
            "keys.json",
            "\"codeName\": \"A\"",
            "\"codeName\": \"Equals\"",
            "CLCC008",
            "\"Equals\"",
            0
        ),
        ["code-name-in-lower-case"] = new(
            "keys.json",
            "\"codeName\": \"A\"",
            "\"codeName\": \"a\"",
            "CLCC008",
            "\"codeName\": \"a\"",
            12
        ),
        ["code-name-reserved-by-generated-type"] = new(
            "keys.json",
            "\"codeName\": \"A\"",
            "\"codeName\": \"All\"",
            "CLCC008",
            "\"All\"",
            0
        ),
        ["timing-group-named-like-its-class"] = new(
            "timings.json",
            "\"App\": {",
            "\"Timings\": {",
            "CLCC008",
            "\"Timings\": {",
            11
        ),
        ["undeclared-key-group"] = new(
            "keys.json",
            "\"group\": \"letters\", \"label\": { \"es\": \"A\"",
            "\"group\": \"digits\", \"label\": { \"es\": \"A\"",
            "CLCC009",
            "\"digits\"",
            0
        ),
        ["side-of-unknown-key"] = new(
            "keys.json",
            "\"sideOf\": \"ctrl\"",
            "\"sideOf\": \"control\"",
            "CLCC009",
            "\"control\"",
            0
        ),
        ["unknown-modifier"] = new(
            "keys.json",
            "\"modifier\": \"ctrl\" }",
            "\"modifier\": \"hyper\" }",
            "CLCC009",
            "\"hyper\"",
            0
        ),
        ["two-value-kinds"] = new(
            "timings.json",
            "\"px\": 60,",
            "\"px\": 60, \"count\": 1,",
            "CLCC009",
            "{ \"px\": 60, \"count\"",
            0
        ),
        ["undefined-default-preset"] = new(
            "touch-presets.json",
            "\"default\": \"mild-tremor\"",
            "\"default\": \"strong\"",
            "CLCC009",
            "\"strong\"",
            0
        ),
    };

    public static TheoryData<string> CaseNames() => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Data_errors_are_reported_at_their_position_in_the_catalog(string caseName)
    {
        var @case = Cases[caseName];
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["keys.json"] = SampleCatalogs.Keys,
            ["keys.win32.json"] = SampleCatalogs.Win32,
            ["timings.json"] = SampleCatalogs.Timings,
            ["touch-presets.json"] = SampleCatalogs.TouchPresets,
        };
        sources[@case.File]
            .ShouldContain(
                @case.Find,
                Case.Sensitive,
                "the sample must contain the text the case replaces"
            );
        sources[@case.File] = sources[@case.File]
            .Replace(@case.Find, @case.Replace, StringComparison.Ordinal);

        var run = CatalogGeneratorHarness.Run(
            SampleCatalogs.All(
                sources["keys.json"],
                sources["keys.win32.json"],
                sources["timings.json"],
                sources["touch-presets.json"]
            )
        );

        run.Result.Results.ShouldAllBe(
            r => r.Exception == null,
            caseName + ": a data error must be a diagnostic, never a generator crash"
        );
        var reportedIn = @case.ReportedIn ?? @case.File;
        var (line, column) = Position(sources[reportedIn], @case.At, @case.Offset);
        var matching = run
            .Diagnostics.Where(d => string.Equals(d.Id, @case.Id, StringComparison.Ordinal))
            .ToList();
        matching.ShouldNotBeEmpty(
            $"{caseName}: expected {@case.Id}, got {string.Join("; ", run.Diagnostics)}"
        );
        matching.ShouldContain(
            d =>
                d.Severity == DiagnosticSeverity.Error
                && string.Equals(
                    Path.GetFileName(d.Location.GetLineSpan().Path),
                    reportedIn,
                    StringComparison.Ordinal
                )
                && d.Location.GetLineSpan().StartLinePosition.Line + 1 == line
                && d.Location.GetLineSpan().StartLinePosition.Character + 1 == column,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{caseName}: expected {@case.Id} at {reportedIn}({line},{column}), got {string.Join("; ", matching)}"
            )
        );
    }

    [Fact]
    public void Missing_catalogs_are_reported_once_each()
    {
        var run = CatalogGeneratorHarness.Run([]);

        run.Diagnostics.ShouldAllBe(d => d.Id == "CLCC010");
        run.Diagnostics.Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(
                [
                    "The catalog 'data/catalogs/keys.json' is not part of the compilation",
                    "The catalog 'data/catalogs/timings.json' is not part of the compilation",
                    "The catalog 'data/catalogs/sizes.json' is not part of the compilation",
                    "The catalog 'data/catalogs/touch-presets.json' is not part of the compilation",
                ],
                ignoreOrder: true
            );
        run.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void Without_keys_win32_json_the_mapping_is_left_to_the_data_tests()
    {
        var catalogs = SampleCatalogs
            .All()
            .Where(c =>
                !string.Equals(
                    Path.GetFileName(c.Path),
                    "keys.win32.json",
                    StringComparison.Ordinal
                )
            );

        var run = CatalogGeneratorHarness.Run(catalogs);

        run.Diagnostics.ShouldBeEmpty();
        run.Sources.ContainsKey("Clicalo.Domain.Keys.KeyIds.g.cs").ShouldBeTrue();
    }

    /// <summary>1-based line and column of the first occurrence of <paramref name="text"/>, plus an offset.</summary>
    private static (int Line, int Column) Position(string content, string text, int offset)
    {
        var index = content.IndexOf(text, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, "position text not found: " + text);
        content
            .IndexOf(text, index + 1, StringComparison.Ordinal)
            .ShouldBe(-1, "position text must be unique: " + text);
        index += offset;
        var lineStart = content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        return (content.AsSpan(0, index).Count('\n') + 1, index - lineStart + 1);
    }

    private sealed record BrokenCatalog(
        string File,
        string Find,
        string Replace,
        string Id,
        string At,
        int Offset,
        string? ReportedIn = null
    );
}
