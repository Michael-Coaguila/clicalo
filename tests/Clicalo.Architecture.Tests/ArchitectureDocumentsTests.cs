using System.Text.Json;
using System.Text.RegularExpressions;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;
using Json.Schema;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// The files under architecture/ validate against their JSON Schema, load strictly into their model and stay
/// consistent with the blueprint and the requirement catalog.
/// </summary>
public sealed class ArchitectureDocumentsTests
{
    private static readonly string[] DocumentNames =
    [
        "allowed-dependencies.json",
        "banned-api-exceptions.json",
        "destructive-operations.json",
        "domain-modules.json",
        "sensitive-paths.json",
        "undo-exemptions.json",
    ];

    public static TheoryData<string> Documents => [.. DocumentNames];

    [Theory]
    [MemberData(nameof(Documents))]
    public void Every_document_validates_against_its_schema(string document)
    {
        using var json = JsonDocument.Parse(
            File.ReadAllText(ArchitectureDocuments.PathOf(document))
        );
        var schemaReference = json.RootElement.GetProperty("$schema").GetString();
        schemaReference.ShouldBe(
            "./schemas/" + Path.GetFileNameWithoutExtension(document) + ".schema.json"
        );

        var schema = JsonSchema.FromFile(ArchitectureDocuments.PathOf(schemaReference![2..]));
        var result = schema.Evaluate(
            json.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List }
        );

        result.IsValid.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Every_schema_is_used_by_a_document()
    {
        var schemas = Directory
            .GetFiles(RepoPaths.Combine("architecture", "schemas"), "*.schema.json")
            .Select(path =>
                Path.GetFileName(path).Replace(".schema.json", ".json", StringComparison.Ordinal)
            );

        schemas.ShouldBe(DocumentNames, ignoreOrder: true);
    }

    [Fact]
    public void Every_document_loads_strictly_into_its_model()
    {
        ArchitectureDocuments.AllowedDependencies().Projects.ShouldNotBeEmpty();
        ArchitectureDocuments.BannedApiExceptions().Exceptions.ShouldNotBeEmpty();
        ArchitectureDocuments.DestructiveOperations().Commands.ShouldNotBeEmpty();
        ArchitectureDocuments.DomainModules().Modules.ShouldNotBeEmpty();
        ArchitectureDocuments.SensitivePaths().Paths.ShouldNotBeEmpty();
        ArchitectureDocuments.UndoExemptions().Exemptions.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public void The_destructive_operations_are_the_closed_list_of_the_blueprint()
    {
        var registry = ArchitectureDocuments.DestructiveOperations();

        registry.Requirement.ShouldBe("REG-04");
        registry
            .Commands.Select(c => c.Name)
            .ShouldBe(
                [
                    "DeleteShortcut",
                    "DeleteProfile",
                    "DeleteDuplicate",
                    "ResetFrequents",
                    "ReplaceOnImport",
                    "RestoreBackup",
                    "DeleteMacroStep",
                ],
                ignoreOrder: true
            );
        registry
            .UseCases.Select(c => c.Name)
            .ShouldBe(
                ["RollbackVersion", "UninstallKeepOrDeleteData", "UninstallSystemComponent"],
                ignoreOrder: true
            );
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void The_undo_exemptions_are_the_justified_ones_of_the_blueprint()
    {
        var registry = ArchitectureDocuments.UndoExemptions();

        registry.Requirement.ShouldBe("REG-07");
        registry
            .Exemptions.Select(e => e.Command)
            .ShouldBe(
                ["RecordUsage", "SetPanelPosition", "SetSetting", "FinishOnboarding"],
                ignoreOrder: true
            );
        registry
            .Exemptions.Single(e =>
                string.Equals(e.Command, "SetSetting", StringComparison.Ordinal)
            )
            .Scope.ShouldBe("descriptorNotUndoable");
        registry.Exemptions.ShouldAllBe(e => e.Justification.Contains('§'));
    }

    [Fact]
    public void Every_requirement_referenced_by_a_registry_exists_in_the_catalog()
    {
        var catalog = File.ReadAllText(RepoPaths.Combine("docs", "requirements", "catalog.md"));

        foreach (
            var requirement in new[]
            {
                ArchitectureDocuments.DestructiveOperations().Requirement,
                ArchitectureDocuments.UndoExemptions().Requirement,
            }
        )
        {
            catalog.ShouldContain("**" + requirement + " · MUST", Case.Sensitive);
        }
    }

    [Fact]
    public void Sensitive_paths_cover_the_trust_boundaries_named_by_the_blueprint()
    {
        var globs = ArchitectureDocuments
            .SensitivePaths()
            .Paths.Select(p => new Glob(p.Pattern))
            .ToList();

        string[] mustBeSensitive =
        [
            "src/Clicalo.Platform.Core/Trust/PinnedKeys.cs",
            "src/Clicalo.Launcher/Program.cs",
            "src/Clicalo.Infrastructure/Persistence/Dto/DocumentDto.cs",
            "data/schemas/document.schema.json",
            "docs/architecture/contracts.md",
            "src/Clicalo.Platform.Core/Ipc/IpcRequest.cs",
            "LICENSE",
            "architecture/sensitive-paths.json",
        ];
        foreach (var path in mustBeSensitive)
        {
            globs.ShouldContain(glob => glob.IsMatch(path), customMessage: path);
        }

        globs.ShouldNotContain(glob =>
            glob.IsMatch("src/Clicalo.Presentation/Panel/PanelViewModel.cs")
        );
    }

    [Fact]
    public void Sensitive_path_patterns_are_unique() =>
        ArchitectureDocuments
            .SensitivePaths()
            .Paths.Select(p => p.Pattern)
            .ShouldBeUnique(StringComparer.Ordinal);

    [Fact]
    public void Every_banned_api_exception_is_under_src_and_named_once()
    {
        var registry = ArchitectureDocuments.BannedApiExceptions();

        registry.Exceptions.Select(e => e.Id).ShouldBeUnique(StringComparer.Ordinal);
        registry
            .Exceptions.SelectMany(e => e.Paths)
            .ShouldAllBe(path => path.StartsWith("src/", StringComparison.Ordinal));
        registry.Exceptions.ShouldAllBe(e =>
            Regex.IsMatch(e.Blueprint, "^§[0-9]", RegexOptions.None, TimeSpan.FromSeconds(1))
        );
    }

    [Fact]
    public void Glob_patterns_match_segments_as_documented()
    {
        new Glob("src/Clicalo.App/Lifecycle/**")
            .IsMatch("src/Clicalo.App/Lifecycle/AppLifetime.cs")
            .ShouldBeTrue();
        new Glob("src/Clicalo.App/Lifecycle/**")
            .IsMatch("src/Clicalo.App/Lifecycle/Deep/X.cs")
            .ShouldBeTrue();
        new Glob("src/Clicalo.App/Lifecycle/**")
            .IsMatch("src/Clicalo.App/LifecycleX/X.cs")
            .ShouldBeFalse();
        new Glob("src/*/Program.cs").IsMatch("src/Clicalo.App/Program.cs").ShouldBeTrue();
        new Glob("src/*/Program.cs").IsMatch("src/Clicalo.App/Deep/Program.cs").ShouldBeFalse();
        new Glob("src/**/Program.cs").IsMatch("src/Program.cs").ShouldBeTrue();
        new Glob(".github/workflows/release*.yml")
            .IsMatch(".github/workflows/release-core.yml")
            .ShouldBeTrue();
        new Glob("LICENSE").IsMatch("license").ShouldBeTrue();
    }

    private static string Describe(EvaluationResults result) =>
        string.Join(
            Environment.NewLine,
            (result.Details ?? [])
                .Where(detail => detail.Errors is { Count: > 0 })
                .SelectMany(detail =>
                    detail.Errors!.Select(error => detail.InstanceLocation + ": " + error.Value)
                )
        );
}
