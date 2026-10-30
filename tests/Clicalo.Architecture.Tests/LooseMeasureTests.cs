using System.Globalization;
using System.Text.Json;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// No new loose measure in <c>Clicalo.UI.Wpf</c> (D12, direction C): sizes, margins, radii and type sizes come from the
/// design tokens (<c>Space</c>, <c>ControlHeight</c>, <c>Radii</c>, <c>IconBox</c>, <c>Stroke</c>,
/// <c>ComponentSize</c>, <c>TextStyles</c>). The literals that remain today are a baseline per file in
/// <c>architecture/loose-measures.json</c>, which can only go down: a file with more literals than its baseline
/// fails, and so does a baseline that is higher than its file, so every reduction is recorded.
/// </summary>
public sealed class LooseMeasureTests
{
    private const string Folder = "src/Clicalo.UI.Wpf";

    [Fact]
    [Trait("Req", "TEM-002")]
    public void No_file_of_the_UI_has_more_loose_measures_than_its_baseline_and_no_baseline_is_stale()
    {
        var baseline = Baseline();
        var actual = Count();
        var problems = new List<string>();
        foreach (var (file, found) in actual.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var allowed = baseline.GetValueOrDefault(file);
            if (found.Count > allowed)
            {
                var lines = string.Join(
                    ", ",
                    found.Skip(0).Take(12).Select(f => f.Line + ": " + f.Literal)
                );
                problems.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{file} has {found.Count} loose measures, {allowed} allowed; use the tokens of data/tokens/design-system.json (lines {lines})."
                    )
                );
            }
            else if (found.Count < allowed)
            {
                problems.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{file} has {found.Count} loose measures: lower its baseline in architecture/loose-measures.json from {allowed} to {found.Count}{(found.Count == 0 ? " (remove the entry)" : string.Empty)}."
                    )
                );
            }
        }

        foreach (var file in baseline.Keys.Where(file => !actual.ContainsKey(file)))
        {
            problems.Add(
                file + " has no loose measures or no longer exists: remove its baseline entry."
            );
        }

        problems.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TEM-002")]
    public void The_base_controls_have_no_loose_measure()
    {
        var baseline = Baseline();

        baseline
            .Keys.Where(file =>
                file.StartsWith(Folder + "/Controls/", StringComparison.Ordinal)
                || file.StartsWith(Folder + "/Windowing/TitleBar", StringComparison.Ordinal)
                || file.StartsWith(Folder + "/Controls/Caption", StringComparison.Ordinal)
            )
            .ShouldBeEmpty("the base controls of direction C are built only with tokens");
    }

    [Theory]
    [InlineData("var a = new Thickness(12, 0, 12, 0);", 2)]
    [InlineData("var a = new Thickness(Space.S3);", 0)]
    [InlineData("button.Height = 48;", 1)]
    [InlineData("button.Height = ControlHeight.Medium;", 0)]
    [InlineData("var b = new Border { Margin = new(0, 8, 0, 0), Width = 2 * 20 };", 3)]
    [InlineData("var c = new GridLength(1, GridUnitType.Star);", 0)]
    [InlineData("var c = new GridLength(220);", 1)]
    [InlineData("var t = Ui.Text(label, 14, bold: true);", 1)]
    [InlineData("var i = Ui.Button(content, name, click, height: 44, radius: 10);", 2)]
    [InlineData("var x = items[i + 1] * 3;", 0)]
    public void Literals_count_only_where_a_measure_goes(string statement, int expected)
    {
        var source = "class C { void M() { " + statement + " } }";

        LooseMeasures.In(source).Count.ShouldBe(expected, statement);
    }

    [Fact]
    public void A_named_double_constant_is_a_loose_measure() =>
        LooseMeasures
            .In("class C { private const double TitleHeight = 52; private const int Count = 3; }")
            .Count.ShouldBe(1);

    private static Dictionary<string, List<(int Line, string Literal)>> Count()
    {
        var root = RepoPaths.Combine("src", "Clicalo.UI.Wpf");
        var result = new Dictionary<string, List<(int, string)>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoPaths.Root, path).Replace('\\', '/');
            if (
                relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal)
            )
            {
                continue;
            }

            var found = LooseMeasures.In(File.ReadAllText(path));
            if (found.Count > 0)
            {
                result[relative] = [.. found];
            }
        }

        return result;
    }

    private static Dictionary<string, int> Baseline()
    {
        using var json = JsonDocument.Parse(
            File.ReadAllText(RepoPaths.Combine("architecture", "loose-measures.json"))
        );
        return json
            .RootElement.GetProperty("baseline")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
    }
}
