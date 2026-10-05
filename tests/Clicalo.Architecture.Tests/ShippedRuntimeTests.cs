using System.Text.Json;
using System.Xml.Linq;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// Every executable that Clícalo publishes restores the graph of every shipped runtime (blueprint §11,
/// docs/architecture/tooling.md, «Runtimes publicados»). An executable that does not declare them gets only the
/// runtime of the machine that restored it (PublishAot adds it implicitly), so its lock file holds one graph and the
/// locked restore of CI fails with NU1004 on the other architecture. Clicalo.Launcher shipped that way from the
/// scaffold until the arm64 jobs first ran.
/// </summary>
public sealed class ShippedRuntimeTests
{
    private const string Tfm = "net10.0-windows10.0.19041";

    public static TheoryData<string> Executables()
    {
        var data = new TheoryData<string>();
        foreach (var project in ExecutableProjects())
        {
            data.Add(Path.GetFileNameWithoutExtension(project));
        }

        return data;
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void The_published_executables_are_the_three_known_ones()
    {
        ExecutableProjects()
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["Clicalo.App", "Clicalo.Launcher", "Clicalo.Sentinel"]);
    }

    [Theory]
    [MemberData(nameof(Executables))]
    [Trait("Req", "NFR-014")]
    public void Every_published_executable_declares_the_shipped_runtimes(string project)
    {
        var properties = XDocument
            .Load(ProjectPath(project))
            .Descendants()
            .Where(static e => Named(e.Parent, "PropertyGroup"))
            .ToList();

        properties
            .Where(static e => Named(e, "RuntimeIdentifiers"))
            .Select(static e => e.Value.Trim())
            .ShouldBe(
                ["$(ClicaloRuntimeIdentifiers)"],
                $"{project} must declare <RuntimeIdentifiers>$(ClicaloRuntimeIdentifiers)</RuntimeIdentifiers>"
            );
        properties
            .Where(static e => Named(e, "RuntimeIdentifier"))
            .Select(static e => (e.Value.Trim(), (string?)e.Attribute("Condition")))
            .ShouldBe(
                [("$(ClicaloRuntimeIdentifier)", "'$(ClicaloRuntimeIdentifier)' != ''")],
                $"{project} must take its RuntimeIdentifier from ClicaloRuntimeIdentifier, never from -r"
            );
    }

    [Theory]
    [MemberData(nameof(Executables))]
    [Trait("Req", "NFR-014")]
    public void Every_published_executable_locks_the_graph_of_every_shipped_runtime(string project)
    {
        using var lockFile = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(Path.GetDirectoryName(ProjectPath(project))!, "packages.lock.json")
            )
        );
        var targets = lockFile
            .RootElement.GetProperty("dependencies")
            .EnumerateObject()
            .Select(static p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var runtime in ShippedRuntimes())
        {
            targets
                .Contains($"{Tfm}/{runtime}")
                .ShouldBeTrue(
                    $"{project}/packages.lock.json has no {Tfm}/{runtime} graph: regenerate it with dotnet restore --force-evaluate"
                );
        }
    }

    [Fact]
    [Trait("Req", "NFR-014")]
    public void The_shipped_runtimes_are_x64_and_arm64()
    {
        ShippedRuntimes().ShouldBe(["win-x64", "win-arm64"]);
    }

    /// <summary>The projects under <c>src/</c> whose output is an executable: the ones that are published.</summary>
    private static IEnumerable<string> ExecutableProjects() =>
        Directory
            .EnumerateFiles(RepoPaths.Combine("src"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path =>
                XDocument
                    .Load(path)
                    .Descendants()
                    .Any(static e => Named(e, "OutputType") && e.Value.Trim() is "Exe" or "WinExe")
            );

    private static bool Named(XElement? element, string name) =>
        element is not null
        && string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);

    private static string ProjectPath(string project) =>
        RepoPaths.Combine("src", project, project + ".csproj");

    private static string[] ShippedRuntimes() =>
        XDocument
            .Load(RepoPaths.Combine("Directory.Build.props"))
            .Descendants()
            .Single(static e => Named(e, "ClicaloRuntimeIdentifiers"))
            .Value.Split(
                ';',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            );
}
