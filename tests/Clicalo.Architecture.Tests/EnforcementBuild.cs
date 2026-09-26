using System.Collections.Immutable;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// Generates throwaway projects under <c>artifacts/tmp</c> that exercise the build-time enforcement
/// (Directory.Build.targets) and builds them all with one <c>dotnet build</c>. Each scenario lives in its own folder
/// with its own artifacts, so several scenarios can reuse a product project name without colliding with each other or
/// with the real build. The projects inherit the repository's Directory.Build.props, Directory.Build.targets,
/// Directory.Packages.props and nuget.config, exactly like a real project.
/// </summary>
public sealed class EnforcementBuild : IAsyncLifetime
{
    private const string PortableTfm = "net10.0";
    private const string WindowsTfm = "net10.0-windows10.0.19041.0";

    private readonly string _root = RepoPaths.Combine(
        "artifacts",
        "tmp",
        "architecture-tests",
        DateTime.UtcNow.ToString(
            "yyyyMMddHHmmss",
            System.Globalization.CultureInfo.InvariantCulture
        )
            + "-"
            + Guid.NewGuid().ToString("N")[..8]
    );

    private readonly List<string> _solutionProjects = [];

    private CliResult? _result;

    /// <summary>Result of the single build of every scenario.</summary>
    internal CliResult Result =>
        _result ?? throw new InvalidOperationException("The build has not run.");

    /// <summary>Diagnostics reported for the project of <paramref name="scenario"/>.</summary>
    internal ImmutableArray<Diagnostic> DiagnosticsOf(string scenario) =>
        [
            .. Result.Diagnostics.Where(d =>
                Path.GetFullPath(d.Project.Length > 0 ? d.Project : d.File)
                    .StartsWith(
                        ScenarioDirectory(scenario) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase
                    )
            ),
        ];

    /// <summary>True when the scenario's project produced its assembly (it compiled).</summary>
    internal bool Compiled(string scenario, string project) =>
        File.Exists(
            Path.Combine(
                ScenarioDirectory(scenario),
                "out",
                "bin",
                project,
                "debug",
                project + ".dll"
            )
        );

    /// <summary>Full path of a file of a scenario.</summary>
    internal string PathOf(string scenario, params string[] parts) =>
        Path.Combine([ScenarioDirectory(scenario), .. parts]);

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        WriteSharedFiles();
        WriteReferenceScenarios();
        WriteBannedApiScenarios();
        // One solution folder per scenario: a solution cannot hold two projects with the same name in one folder.
        await File.WriteAllTextAsync(
            Path.Combine(_root, "scenarios.slnx"),
            "<Solution>\n"
                + string.Concat(
                    _solutionProjects.Select(p =>
                        "  <Folder Name=\"/"
                        + p.Split('/')[0]
                        + "/\">\n    <Project Path=\""
                        + p
                        + "\" />\n  </Folder>\n"
                    )
                )
                + "</Solution>\n",
            TestContext.Current.CancellationToken
        );

        _result = await DotnetCli.RunAsync(
            _root,
            ["build", "scenarios.slnx", "-nodeReuse:false", "-v:m", "-clp:NoSummary;ForceNoAlign"],
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken
        );
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: artifacts/tmp is ignored by git and cleaned with artifacts/.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }

        return ValueTask.CompletedTask;
    }

    private string ScenarioDirectory(string scenario) =>
        Path.GetFullPath(Path.Combine(_root, scenario));

    private void WriteSharedFiles() =>
        Write(
            "Directory.Build.props",
            """
            <Project>
              <!-- Repository settings, then per-scenario artifacts and no lock files or audit for throwaway projects. -->
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
              <PropertyGroup>
                <ArtifactsPath>$([System.IO.Path]::GetFullPath('$(MSBuildProjectDirectory)\..\out'))</ArtifactsPath>
                <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
                <RestoreLockedMode>false</RestoreLockedMode>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
            </Project>
            """
        );

    private void WriteReferenceScenarios()
    {
        Project(
            "forbidden-project",
            "Clicalo.Domain",
            PortableTfm,
            items: """<ProjectReference Include="..\Clicalo.Infrastructure\Clicalo.Infrastructure.csproj" />"""
        );
        Project("forbidden-project", "Clicalo.Infrastructure", PortableTfm, inSolution: false);

        Project(
            "forbidden-package",
            "Clicalo.Domain",
            PortableTfm,
            items: """<PackageReference Include="Serilog" />"""
        );

        Project(
            "forbidden-framework",
            "Clicalo.Presentation",
            WindowsTfm,
            properties: "<UseWPF>true</UseWPF>"
        );

        Project(
            "analyzer-misuse",
            "Clicalo.Domain",
            PortableTfm,
            items: """<ProjectReference Include="..\Clicalo.Infrastructure\Clicalo.Infrastructure.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />"""
        );
        Project("analyzer-misuse", "Clicalo.Infrastructure", PortableTfm, inSolution: false);

        Project("undeclared", "Clicalo.Rogue", PortableTfm);

        Project(
            "allowed",
            "Clicalo.Application",
            PortableTfm,
            items: """
            <ProjectReference Include="..\Clicalo.Domain\Clicalo.Domain.csproj" />
            <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
            """
        );
        Project("allowed", "Clicalo.Domain", PortableTfm, inSolution: false);
    }

    private void WriteBannedApiScenarios()
    {
        Project("banned-domain", "Clicalo.Domain", PortableTfm, properties: Product);
        Write("banned-domain/Clicalo.Domain/Probe.cs", BannedApiProbes.Domain);

        Project(
            "banned-surfaces",
            "Clicalo.UI.Wpf",
            WindowsTfm,
            properties: Product
                + "<UseWPF>true</UseWPF><AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
            items: """<PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />"""
        );
        Write("banned-surfaces/Clicalo.UI.Wpf/NativeMethods.txt", BannedApiProbes.NativeMethods);
        Write("banned-surfaces/Clicalo.UI.Wpf/Probe.cs", BannedApiProbes.Surfaces);

        Project("not-product", "Clicalo.DevCli", PortableTfm);
        Write("not-product/Clicalo.DevCli/Probe.cs", BannedApiProbes.Tool);
    }

    private const string Product = "<ClicaloIsProductProject>true</ClicaloIsProductProject>";

    private void Project(
        string scenario,
        string name,
        string targetFramework,
        string properties = "",
        string items = "",
        bool inSolution = true
    )
    {
        var relative = scenario + "/" + name + "/" + name + ".csproj";
        Write(
            relative,
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                + "  <PropertyGroup>\n"
                + "    <TargetFramework>"
                + targetFramework
                + "</TargetFramework>\n"
                + "    "
                + properties
                + "\n"
                + "  </PropertyGroup>\n"
                + "  <ItemGroup>\n"
                + "    "
                + items
                + "\n"
                + "  </ItemGroup>\n"
                + "</Project>\n"
        );
        if (inSolution)
        {
            _solutionProjects.Add(relative);
        }
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
    }
}

/// <summary>Shares one <see cref="EnforcementBuild"/> between the slow build tests.</summary>
[CollectionDefinition(Name)]
public sealed class SharedEnforcementBuild : ICollectionFixture<EnforcementBuild>
{
    public const string Name = "Enforcement build";
}
