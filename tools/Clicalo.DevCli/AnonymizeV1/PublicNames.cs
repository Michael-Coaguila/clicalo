using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>
/// What <c>anonymize-v1</c> may keep as written: generic shortcut and profile names, and public program names
/// (<c>v1-public-names.json</c>, embedded and reviewed like code). Everything else is replaced.
/// </summary>
/// <param name="Names">Shortcut labels and profile names that name a function or an app, never a person.</param>
/// <param name="Programs">Executable names of public apps and interpreters, in lower case, with <c>.exe</c>.</param>
internal sealed record PublicNames(FrozenSet<string> Names, FrozenSet<string> Programs)
{
    private const string ResourceName = "Clicalo.DevCli.AnonymizeV1.v1-public-names.json";

    /// <summary>The reviewed list embedded in the tool.</summary>
    public static PublicNames Embedded { get; } = Load();

    /// <summary>Whether a program (with or without <c>.exe</c>, any case, with or without path) is public.</summary>
    /// <param name="program">The program text.</param>
    public bool IsPublicProgram(string program)
    {
        var name = program.Trim().Trim('"');
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        return Programs.Contains(name) || Programs.Contains(name + ".exe");
    }

    /// <summary>Builds a list from JSON text (tests use their own).</summary>
    /// <param name="json">An object with the arrays <c>names</c> and <c>programs</c>.</param>
    public static PublicNames Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new PublicNames(
            Strings(root, "names").ToFrozenSet(StringComparer.Ordinal),
            Strings(root, "programs").ToFrozenSet(StringComparer.OrdinalIgnoreCase)
        );
    }

    private static PublicNames Load()
    {
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded " + ResourceName + " is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static IEnumerable<string> Strings(JsonElement root, string property) =>
        root.GetProperty(property)
            .EnumerateArray()
            .Select(static e => e.GetString() ?? string.Empty);
}
