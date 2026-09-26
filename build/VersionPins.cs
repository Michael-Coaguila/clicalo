using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Clicalo.Build;

/// <summary>
/// Enforces reproducible builds (NFR-014) and the supply-chain rules of blueprint Â§2.1, Â§10.5 and Â§12.2 T12:
/// exact package versions only in <c>Directory.Packages.props</c>, exact tool and SDK versions, and every
/// GitHub action pinned by full commit SHA with a version comment.
/// </summary>
internal static partial class VersionPins
{
    private const string CentralFileName = "Directory.Packages.props";

    private static readonly HashSet<string> SkippedDirectoryNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "artifacts",
        ".git",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "node_modules",
        "TestResults",
    };

    private static readonly HashSet<string> ProjectExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".csproj",
        ".props",
        ".targets",
    };

    /// <summary>Checks the whole repository.</summary>
    public static IReadOnlyList<VersionPinViolation> Check(RepoLayout layout)
    {
        var violations = new List<VersionPinViolation>();

        var central = Path.Combine(layout.Root, CentralFileName);
        violations.AddRange(
            CheckCentralPackages(layout.RelativeForward(central), LoadXml(central))
        );

        foreach (var file in EnumerateFiles(layout))
        {
            var relative = layout.RelativeForward(file);
            var extension = Path.GetExtension(file);
            var name = Path.GetFileName(file);
            if (
                ProjectExtensions.Contains(extension)
                && !string.Equals(name, CentralFileName, StringComparison.Ordinal)
            )
            {
                violations.AddRange(CheckProjectFile(relative, LoadXml(file)));
            }
            else if (IsWorkflowOrAction(relative))
            {
                violations.AddRange(CheckWorkflow(relative, File.ReadAllText(file)));
            }
        }

        var tools = Path.Combine(layout.Root, ".config", "dotnet-tools.json");
        if (File.Exists(tools))
        {
            violations.AddRange(
                CheckToolManifest(layout.RelativeForward(tools), File.ReadAllText(tools))
            );
        }

        var globalJson = Path.Combine(layout.Root, "global.json");
        violations.AddRange(
            CheckGlobalJson(layout.RelativeForward(globalJson), File.ReadAllText(globalJson))
        );

        return violations;
    }

    /// <summary>
    /// Whether <paramref name="version"/> is one exact NuGet/SemVer version: <c>1.2.3</c> or the strict
    /// <c>[1.2.3]</c>, never a range, a wildcard or empty.
    /// </summary>
    public static bool IsExactVersion(string? version)
    {
        if (version is null)
        {
            return false;
        }

        var candidate =
            version.Length > 2 && version[0] == '[' && version[^1] == ']'
                ? version[1..^1]
                : version;
        return ExactVersion().IsMatch(candidate);
    }

    /// <summary>Every central version must be exact.</summary>
    public static IEnumerable<VersionPinViolation> CheckCentralPackages(
        string file,
        XDocument document
    )
    {
        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName is not ("PackageVersion" or "GlobalPackageReference"))
            {
                continue;
            }

            var id = (string?)element.Attribute("Include") ?? element.Name.LocalName;
            var version = (string?)element.Attribute("Version");
            if (version is null)
            {
                yield return new VersionPinViolation(
                    file,
                    LineOf(element),
                    id,
                    Messages.PinMissingVersion
                );
            }
            else if (!IsExactVersion(version))
            {
                yield return new VersionPinViolation(
                    file,
                    LineOf(element),
                    id + " " + version,
                    Messages.PinFloatingVersion
                );
            }
        }
    }

    /// <summary>Project files reference packages without versions; versions live only in the central file.</summary>
    public static IEnumerable<VersionPinViolation> CheckProjectFile(string file, XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            var kind = element.Name.LocalName;
            if (kind is "PackageVersion")
            {
                var id = (string?)element.Attribute("Include") ?? kind;
                yield return new VersionPinViolation(
                    file,
                    LineOf(element),
                    id,
                    Messages.PinVersionOutsideCentral
                );
                continue;
            }

            if (kind is not ("PackageReference" or "GlobalPackageReference"))
            {
                continue;
            }

            var hasVersion =
                element.Attribute("Version") is not null
                || element.Attribute("VersionOverride") is not null
                || element.Element(element.Name.Namespace + "Version") is not null
                || element.Element(element.Name.Namespace + "VersionOverride") is not null;
            if (hasVersion)
            {
                var id =
                    (string?)element.Attribute("Include")
                    ?? (string?)element.Attribute("Update")
                    ?? kind;
                yield return new VersionPinViolation(
                    file,
                    LineOf(element),
                    id,
                    Messages.PinVersionOutsideCentral
                );
            }
        }
    }

    /// <summary>Every local tool has an exact version.</summary>
    public static IReadOnlyList<VersionPinViolation> CheckToolManifest(string file, string json)
    {
        var violations = new List<VersionPinViolation>();
        using var document = JsonDocument.Parse(json);
        if (
            !document.RootElement.TryGetProperty("tools", out var tools)
            || tools.ValueKind != JsonValueKind.Object
        )
        {
            return violations;
        }

        foreach (var tool in tools.EnumerateObject())
        {
            var version =
                tool.Value.TryGetProperty("version", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            if (!IsExactVersion(version))
            {
                violations.Add(
                    new VersionPinViolation(
                        file,
                        null,
                        version is null ? tool.Name : tool.Name + " " + version,
                        version is null ? Messages.PinMissingVersion : Messages.PinFloatingVersion
                    )
                );
            }
        }

        return violations;
    }

    /// <summary>The SDK is pinned to one exact version.</summary>
    public static IReadOnlyList<VersionPinViolation> CheckGlobalJson(string file, string json)
    {
        using var document = JsonDocument.Parse(json);
        var version =
            document.RootElement.TryGetProperty("sdk", out var sdk)
            && sdk.TryGetProperty("version", out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        return IsExactVersion(version)
            ? []
            :
            [
                new VersionPinViolation(
                    file,
                    null,
                    version is null ? "sdk" : "sdk " + version,
                    version is null ? Messages.PinMissingVersion : Messages.PinFloatingVersion
                ),
            ];
    }

    /// <summary>Every external action is pinned by a full commit SHA followed by a version comment.</summary>
    public static IReadOnlyList<VersionPinViolation> CheckWorkflow(string file, string yaml)
    {
        var violations = new List<VersionPinViolation>();
        var lineNumber = 0;
        foreach (var line in Markdown.Lines(yaml).Split('\n'))
        {
            lineNumber++;
            var match = UsesLine().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var reference = match.Groups["reference"].Value;
            if (
                reference.StartsWith("./", StringComparison.Ordinal)
                || reference.StartsWith("docker://", StringComparison.Ordinal)
            )
            {
                continue;
            }

            if (!PinnedAction().IsMatch(reference))
            {
                violations.Add(
                    new VersionPinViolation(file, lineNumber, reference, Messages.PinActionNotSha)
                );
            }
            else if (!match.Groups["comment"].Success)
            {
                violations.Add(
                    new VersionPinViolation(
                        file,
                        lineNumber,
                        reference,
                        Messages.PinActionNoVersionComment
                    )
                );
            }
        }

        return violations;
    }

    /// <summary>Renders the violations as a list for the failure report.</summary>
    public static string Render(IReadOnlyList<VersionPinViolation> violations) =>
        string.Concat(
            violations.Select(violation =>
                "- "
                + Markdown.Text(Messages.LineColumn(violation.File, violation.Line, column: null))
                + ": "
                + Markdown.Text(violation.Subject)
                + ", "
                + violation.Problem
                + "\n"
            )
        );

    private static bool IsWorkflowOrAction(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        var isYaml =
            string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase);
        return isYaml
            && (
                relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(".github/actions/", StringComparison.OrdinalIgnoreCase)
            );
    }

    private static IEnumerable<string> EnumerateFiles(RepoLayout layout)
    {
        var handoff = Path.Combine(layout.Root, "docs", "design", "handoff");
        var pending = new Stack<string>();
        pending.Push(layout.Root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);
                if (
                    SkippedDirectoryNames.Contains(name)
                    || string.Equals(child, handoff, StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }

    private static XDocument LoadXml(string path) => XDocument.Load(path, LoadOptions.SetLineInfo);

    private static int? LineOf(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;

    [GeneratedRegex(
        @"^\d+(\.\d+){1,3}(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ExactVersion();

    [GeneratedRegex(
        @"^\s*(-\s+)?uses:\s*['""]?(?<reference>[^'""\s#]+)['""]?\s*(?<comment>#\s*\S+.*)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex UsesLine();

    [GeneratedRegex(
        "^[^@\\s]+@[0-9a-f]{40}$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex PinnedAction();
}
