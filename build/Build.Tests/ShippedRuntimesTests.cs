using System.Text.Json;
using System.Xml.Linq;

namespace Clicalo.Build.Tests;

/// <summary>
/// NFR-014: the published executables lock the graphs of both shipped runtimes (win-x64 and win-arm64), and
/// <c>cl perf</c> publishes with the same restore as any build: locked in CI, without rewriting any lock file.
/// </summary>
[Trait("Req", "NFR-014")]
public sealed class ShippedRuntimesTests
{
    private static readonly string Root = RepoLayout.Locate(AppContext.BaseDirectory).Root;

    public static TheoryData<string> PublishedExecutables =>
        [BuildSteps.AppProject, BuildSteps.SentinelProject];

    [Fact]
    public void Cl_perf_names_the_runtime_without_reaching_the_restore_of_the_libraries()
    {
        var args = BuildSteps.PublishArguments(
            BuildSteps.AppProject,
            "artifacts/perf/apps/sc-r2r",
            PublishVariant.All[0].Properties
        );

        args.ShouldContain(
            "-p:ClicaloRuntimeIdentifier=" + BuildSteps.RuntimeIdentifier,
            StringComparer.Ordinal
        );
        args.ShouldNotContain("-r", StringComparer.Ordinal);
        args.ShouldNotContain("--runtime", StringComparer.Ordinal);
        args.ShouldNotContain(arg =>
            arg.StartsWith("-p:RestorePackagesWithLockFile", StringComparison.OrdinalIgnoreCase)
            || arg.StartsWith("-p:RestoreLockedMode", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Theory]
    [MemberData(nameof(PublishedExecutables))]
    public void A_published_executable_takes_its_runtime_from_the_shipped_list(string project)
    {
        var properties = XDocument
            .Load(Path.Combine(Root, project))
            .Descendants()
            .Where(static element => !element.HasElements)
            .ToList();

        properties
            .Single(static element => Named(element, "RuntimeIdentifiers"))
            .Value.ShouldBe("$(ClicaloRuntimeIdentifiers)");
        var runtime = properties.Single(static element => Named(element, "RuntimeIdentifier"));
        runtime.Value.ShouldBe("$(ClicaloRuntimeIdentifier)");
        runtime.Attribute("Condition")?.Value.ShouldBe("'$(ClicaloRuntimeIdentifier)' != ''");
    }

    [Theory]
    [MemberData(nameof(PublishedExecutables))]
    public void The_lock_file_of_a_published_executable_holds_both_shipped_runtimes(string project)
    {
        var lockFile = Path.Combine(
            Root,
            Path.GetDirectoryName(project) ?? string.Empty,
            "packages.lock.json"
        );
        using var document = JsonDocument.Parse(File.ReadAllText(lockFile));

        var graphs = document
            .RootElement.GetProperty("dependencies")
            .EnumerateObject()
            .Select(static graph => graph.Name)
            .ToList();

        graphs.ShouldContain(static graph => graph.EndsWith("/win-x64", StringComparison.Ordinal));
        graphs.ShouldContain(static graph =>
            graph.EndsWith("/win-arm64", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void The_shipped_runtimes_are_x64_and_arm64()
    {
        var shipped = XDocument
            .Load(Path.Combine(Root, "Directory.Build.props"))
            .Descendants()
            .Single(static element => Named(element, "ClicaloRuntimeIdentifiers"))
            .Value;

        shipped.Split(';').ShouldBe(["win-x64", "win-arm64"]);
        shipped.Split(';').ShouldContain(BuildSteps.RuntimeIdentifier, StringComparer.Ordinal);
    }

    private static bool Named(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);
}
