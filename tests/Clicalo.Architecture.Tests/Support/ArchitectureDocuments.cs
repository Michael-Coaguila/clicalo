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
