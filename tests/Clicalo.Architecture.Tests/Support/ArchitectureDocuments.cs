using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>Strict loader for the files under <c>architecture/</c>.</summary>
internal static class ArchitectureDocuments
{
    /// <summary>Unknown properties, missing required members and nulls in non-nullable members are errors.</summary>
    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
        };

    /// <summary>Absolute path of a file under <c>architecture/</c>.</summary>
    public static string PathOf(string name) => RepoPaths.Combine("architecture", name);

    /// <summary>Reads and deserializes <c>architecture/{name}</c>.</summary>
    public static T Load<T>(string name) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(PathOf(name)), Options)
        ?? throw new InvalidOperationException("architecture/" + name + " is empty.");

    public static AllowedDependencies AllowedDependencies() =>
        Load<AllowedDependencies>("allowed-dependencies.json");

    public static DomainModules DomainModules() => Load<DomainModules>("domain-modules.json");

    public static DestructiveOperations DestructiveOperations() =>
        Load<DestructiveOperations>("destructive-operations.json");

    public static UndoExemptions UndoExemptions() => Load<UndoExemptions>("undo-exemptions.json");

    public static SensitivePaths SensitivePaths() => Load<SensitivePaths>("sensitive-paths.json");

    public static BannedApiExceptions BannedApiExceptions() =>
        Load<BannedApiExceptions>("banned-api-exceptions.json");
}

/// <summary><c>architecture/allowed-dependencies.json</c>.</summary>
internal sealed record AllowedDependencies(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    NamedList AnalyzerProjects,
    NamedPackages GlobalPackages,
    ImmutableSortedDictionary<string, ProjectEntry> Projects
);

internal sealed record NamedList(string Description, ImmutableArray<string> Projects);

internal sealed record NamedPackages(string Description, ImmutableArray<string> Packages);

internal sealed record ProjectEntry(
    string Area,
    string Rule,
    string Platform,
    ImmutableArray<string> ProjectReferences,
    ImmutableArray<string> PackageReferences,
    ImmutableArray<string>? FrameworkReferences = null,
    ImmutableArray<string>? AssemblyReferences = null
);

/// <summary><c>architecture/domain-modules.json</c>.</summary>
internal sealed record DomainModules(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string RootNamespace,
    ImmutableArray<DomainModule> Modules
);

internal sealed record DomainModule(
    string Name,
    string Group,
    string Description,
    ImmutableArray<string> DependsOn,
    string? Deviation = null
);

/// <summary><c>architecture/destructive-operations.json</c>.</summary>
internal sealed record DestructiveOperations(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string Requirement,
    ImmutableArray<Operation> Commands,
    ImmutableArray<Operation> UseCases
);

internal sealed record Operation(string Name, string Description);

/// <summary><c>architecture/undo-exemptions.json</c>.</summary>
internal sealed record UndoExemptions(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    string Requirement,
    ImmutableArray<UndoExemption> Exemptions
);

internal sealed record UndoExemption(string Command, string Scope, string Justification);

/// <summary><c>architecture/sensitive-paths.json</c>.</summary>
internal sealed record SensitivePaths(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    ImmutableArray<SensitivePath> Paths
);

internal sealed record SensitivePath(string Pattern, string Category, string Reason);

/// <summary><c>architecture/banned-api-exceptions.json</c>.</summary>
internal sealed record BannedApiExceptions(
    [property: JsonPropertyName("$schema")] string Schema,
    string Description,
    ImmutableArray<BannedApiException> Exceptions
);

internal sealed record BannedApiException(
    string Id,
    ImmutableArray<string> Paths,
    ImmutableArray<string> Apis,
    string Justification,
    string Blueprint
);
