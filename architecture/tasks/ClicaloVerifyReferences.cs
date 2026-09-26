// Reference whitelist check (blueprint §4.4, mechanism 1; docs/architecture/enforcement.md).
//
// This single file is the source of the ClicaloVerifyReferences MSBuild task. RoslynCodeTaskFactory compiles it on
// demand for the MSBuild that runs the build (.NET in the CLI, .NET Framework in Visual Studio), so it only uses the
// netstandard2.0 surface: no records, no init accessors, no spans and no System.Text.Json. Clicalo.Architecture.Tests
// links the same file with CLICALO_REFERENCE_POLICY_TESTS defined, which removes the thin MSBuild adapter and keeps
// the pure policy, so the policy is unit tested under the repository analyzers.
#nullable enable

using System.Globalization;
using System.Text;
#if !CLICALO_REFERENCE_POLICY_TESTS
// RoslynCodeTaskFactory compiles without implicit usings; the test project has them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endif

namespace Clicalo.Architecture.Tasks;

#if CLICALO_REFERENCE_POLICY_TESTS
/// <summary>Pure reference policy behind the <c>ClicaloVerifyReferences</c> MSBuild task.</summary>
internal static class ClicaloVerifyReferences
#else
/// <summary>
/// MSBuild task that fails the build when a project declares a reference that
/// <c>architecture/allowed-dependencies.json</c> does not allow.
/// </summary>
public sealed class ClicaloVerifyReferences : Task
#endif
{
    /// <summary>Invalid or unreadable policy file.</summary>
    internal const string InvalidPolicyCode = "CLCA000";

    /// <summary>A declared reference is not in the project's whitelist.</summary>
    internal const string ForbiddenReferenceCode = "CLCA001";

    /// <summary>The project is not declared in the policy.</summary>
    internal const string UndeclaredProjectCode = "CLCA002";

    /// <summary>The project targets a platform its layer does not allow.</summary>
    internal const string WrongPlatformCode = "CLCA003";

    internal const string WpfFramework = "Microsoft.WindowsDesktop.App.WPF";
    internal const string WindowsFormsFramework = "Microsoft.WindowsDesktop.App.WindowsForms";

    private const string EnforcementGuide = "docs/architecture/enforcement.md";

#if !CLICALO_REFERENCE_POLICY_TESTS
    /// <summary>Absolute path of <c>architecture/allowed-dependencies.json</c>.</summary>
    [Required]
    public string PolicyFile { get; set; } = string.Empty;

    /// <summary>Repository root, used to show repository-relative paths.</summary>
    [Required]
    public string RepositoryRoot { get; set; } = string.Empty;

    /// <summary>Name of the project being built (<c>$(MSBuildProjectName)</c>).</summary>
    [Required]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Full path of the project being built.</summary>
    [Required]
    public string ProjectFile { get; set; } = string.Empty;

    /// <summary>OS platform of the target framework: empty for portable frameworks, <c>windows</c> otherwise.</summary>
    public string TargetPlatformIdentifier { get; set; } = string.Empty;

    /// <summary><c>$(UseWPF)</c>, which adds the WPF framework reference.</summary>
    public bool UseWpf { get; set; }

    /// <summary><c>$(UseWindowsForms)</c>, which adds the Windows Forms framework reference.</summary>
    public bool UseWindowsForms { get; set; }

    /// <summary>
    /// References declared by repository files, with <c>ClicaloKind</c> (Project, Package, GlobalPackage, Framework
    /// or Assembly) and <c>ClicaloDefiningFile</c> metadata.
    /// </summary>
    public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();

    /// <inheritdoc />
    public override bool Execute()
    {
        var policyPath = Path.GetFullPath(PolicyFile);
        Policy policy;
        try
        {
            policy = Policy.Parse(File.ReadAllText(policyPath));
        }
        catch (PolicyFormatException ex)
        {
            Log.LogError(
                null,
                InvalidPolicyCode,
                null,
                policyPath,
                ex.Line,
                ex.Column,
                0,
                0,
                ex.Message
            );
            return false;
        }
        catch (IOException ex)
        {
            Log.LogError(null, InvalidPolicyCode, null, policyPath, 0, 0, 0, 0, ex.Message);
            return false;
        }

        var project = new ProjectFacts(
            ProjectName,
            ProjectFile,
            TargetPlatformIdentifier,
            UseWpf,
            UseWindowsForms,
            DisplayPath(RepositoryRoot, policyPath)
        );
        var references = References.Select(item =>
        {
            var kind = ParseKind(item.GetMetadata("ClicaloKind"));
            var name = kind switch
            {
                ReferenceKind.Project => item.GetMetadata("Filename"),
                ReferenceKind.Assembly => AssemblyName(item.ItemSpec),
                _ => item.ItemSpec,
            };
            var analyzerOnly = IsAnalyzerOnly(
                item.GetMetadata("OutputItemType"),
                item.GetMetadata("ReferenceOutputAssembly")
            );
            return new DeclaredReference(
                kind,
                name,
                analyzerOnly,
                item.GetMetadata("ClicaloDefiningFile")
            );
        });

        foreach (var finding in Evaluate(policy, project, references))
        {
            Log.LogError(
                null,
                finding.Code,
                null,
                finding.File,
                finding.Line,
                finding.Column,
                0,
                0,
                finding.Message
            );
        }

        return !Log.HasLoggedErrors;
    }
#endif

    /// <summary>Kinds of reference the policy governs.</summary>
    internal enum ReferenceKind
    {
        /// <summary><c>ProjectReference</c>.</summary>
        Project,

        /// <summary><c>PackageReference</c>.</summary>
        Package,

        /// <summary><c>GlobalPackageReference</c> (Directory.Packages.props).</summary>
        GlobalPackage,

        /// <summary><c>FrameworkReference</c>, or <c>UseWPF</c>/<c>UseWindowsForms</c>.</summary>
        Framework,

        /// <summary>Raw assembly <c>Reference</c>.</summary>
        Assembly,
    }

    /// <summary>Maps the <c>ClicaloKind</c> metadata set by Directory.Build.targets to a <see cref="ReferenceKind"/>.</summary>
    internal static ReferenceKind ParseKind(string value) =>
        value switch
        {
            "Project" => ReferenceKind.Project,
            "Package" => ReferenceKind.Package,
            "GlobalPackage" => ReferenceKind.GlobalPackage,
            "Framework" => ReferenceKind.Framework,
            "Assembly" => ReferenceKind.Assembly,
            _ => throw new ArgumentException(
                "Unknown reference kind '" + value + "'.",
                nameof(value)
            ),
        };

    /// <summary>
    /// True for a reference that only loads a Roslyn component into the compiler
    /// (<c>OutputItemType="Analyzer"</c> and <c>ReferenceOutputAssembly="false"</c>).
    /// </summary>
    internal static bool IsAnalyzerOnly(string outputItemType, string referenceOutputAssembly) =>
        string.Equals(outputItemType, "Analyzer", StringComparison.OrdinalIgnoreCase)
        && string.Equals(referenceOutputAssembly, "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Simple name of an assembly <c>Reference</c>: <c>Foo</c> for <c>Foo, Version=1.0</c> and <c>lib\Foo.dll</c>.</summary>
    internal static string AssemblyName(string itemSpec)
    {
        if (itemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(itemSpec);
        }

        var comma = itemSpec.IndexOf(',');
        return (comma < 0 ? itemSpec : itemSpec.Substring(0, comma)).Trim();
    }

    /// <summary>Evaluates the declared references of one project against the policy.</summary>
    internal static IReadOnlyList<Finding> Evaluate(
        Policy policy,
        ProjectFacts project,
        IEnumerable<DeclaredReference> references
    )
    {
        var findings = new List<Finding>();
        if (!policy.Projects.TryGetValue(project.Name, out var rule))
        {
            var message = Format(
                "Project '{0}' is not declared in {1}. Every project declares its area, rule, platform and allowed "
                    + "references there; see {2}.",
                project.Name,
                project.PolicyDisplayPath,
                EnforcementGuide
            );
            findings.Add(new Finding(UndeclaredProjectCode, message, project.ProjectFile, 0, 0));
            return findings;
        }

        var platformFinding = CheckPlatform(rule, project);
        if (platformFinding is not null)
        {
            findings.Add(platformFinding);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in ImpliedFrameworks(project).Concat(references))
        {
            var key = Format("{0}|{1}|{2}", reference.Kind, reference.Name, reference.AnalyzerOnly);
            if (seen.Add(key) && !IsAllowed(policy, rule, reference))
            {
                findings.Add(Forbidden(policy, rule, project, reference));
            }
        }

        return findings;
    }

    private static IEnumerable<DeclaredReference> ImpliedFrameworks(ProjectFacts project)
    {
        if (project.UseWpf)
        {
            yield return new DeclaredReference(
                ReferenceKind.Framework,
                WpfFramework,
                false,
                project.ProjectFile,
                "UseWPF"
            );
        }

        if (project.UseWindowsForms)
        {
            yield return new DeclaredReference(
                ReferenceKind.Framework,
                WindowsFormsFramework,
                false,
                project.ProjectFile,
                "UseWindowsForms"
            );
        }
    }

    private static Finding? CheckPlatform(ProjectRule rule, ProjectFacts project)
    {
        var actual = project.TargetPlatformIdentifier;
        var portable = string.Equals(rule.Platform, Platforms.Portable, StringComparison.Ordinal);
        var ok = portable
            ? actual.Length == 0
            : string.Equals(actual, "windows", StringComparison.OrdinalIgnoreCase);
        if (ok)
        {
            return null;
        }

        var message = Format(
            "Project '{0}' must target {1} but targets {2}. Rule for {0}: {3} See {4}.",
            project.Name,
            portable ? "a portable framework (no OS platform)" : "Windows",
            actual.Length == 0 ? "a portable framework" : "the '" + actual + "' platform",
            rule.Rule,
            EnforcementGuide
        );
        var (line, column) = Locate(project.ProjectFile, "<TargetFramework");
        return new Finding(WrongPlatformCode, message, project.ProjectFile, line, column);
    }

    private static bool IsAllowed(Policy policy, ProjectRule rule, DeclaredReference reference) =>
        reference.Kind switch
        {
            ReferenceKind.Project => rule.ProjectReferences.Contains(reference.Name)
                || (reference.AnalyzerOnly && policy.AnalyzerProjects.Contains(reference.Name)),
            ReferenceKind.Package => rule.PackageReferences.Contains(reference.Name)
                || policy.GlobalPackages.Contains(reference.Name),
            ReferenceKind.GlobalPackage => policy.GlobalPackages.Contains(reference.Name),
            ReferenceKind.Framework => rule.FrameworkReferences.Contains(reference.Name),
            _ => rule.AssemblyReferences.Contains(reference.Name),
        };

    private static Finding Forbidden(
        Policy policy,
        ProjectRule rule,
        ProjectFacts project,
        DeclaredReference reference
    )
    {
        string what;
        string allowedLabel;
        SortedSet<string> allowed;
        switch (reference.Kind)
        {
            case ReferenceKind.Project when reference.AnalyzerOnly:
                what = "Analyzer project reference";
                allowedLabel = "project and analyzer references";
                allowed = new SortedSet<string>(
                    rule.ProjectReferences,
                    StringComparer.OrdinalIgnoreCase
                );
                allowed.UnionWith(policy.AnalyzerProjects);
                break;
            case ReferenceKind.Project:
                (what, allowedLabel, allowed) = (
                    "Project reference",
                    "project references",
                    rule.ProjectReferences
                );
                break;
            case ReferenceKind.Package:
                (what, allowedLabel, allowed) = (
                    "Package reference",
                    "package references",
                    rule.PackageReferences
                );
                break;
            case ReferenceKind.GlobalPackage:
                (what, allowedLabel, allowed) = (
                    "Global package reference",
                    "global packages",
                    policy.GlobalPackages
                );
                break;
            case ReferenceKind.Framework:
                (what, allowedLabel, allowed) = (
                    "Framework reference",
                    "framework references",
                    rule.FrameworkReferences
                );
                break;
            default:
                (what, allowedLabel, allowed) = (
                    "Assembly reference",
                    "assembly references",
                    rule.AssemblyReferences
                );
                break;
        }

        var message = new StringBuilder()
            .Append(what)
            .Append(" '")
            .Append(reference.Name)
            .Append('\'')
            .Append(
                reference.Origin.Length == 0 ? string.Empty : " (" + reference.Origin + "=true)"
            )
            .Append(
                Format(
                    " is not allowed in '{0}'. {1} allows ",
                    project.Name,
                    project.PolicyDisplayPath
                )
            )
            .Append(
                Format(
                    "{0}: {1}. Rule for {2}: {3}",
                    allowedLabel,
                    Describe(allowed),
                    project.Name,
                    rule.Rule
                )
            )
            .Append(
                reference.Kind == ReferenceKind.Assembly
                    ? " Prefer a PackageReference or a ProjectReference."
                    : ""
            )
            .Append(
                Format(
                    " If the dependency is intended, change the whitelist with a justification ({0}).",
                    EnforcementGuide
                )
            )
            .ToString();

        var file =
            reference.DefiningFile.Length == 0 ? project.ProjectFile : reference.DefiningFile;
        var (line, column) = Locate(
            file,
            reference.Origin.Length == 0 ? reference.Name : "<" + reference.Origin
        );
        return new Finding(ForbiddenReferenceCode, message, file, line, column);
    }

    private static string Describe(SortedSet<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>
    /// Best-effort 1-based position of <paramref name="text"/> in <paramref name="file"/>, so IDEs jump to the line
    /// that declares the reference. Returns (0, 0) when the file or the text cannot be found.
    /// </summary>
    internal static (int Line, int Column) Locate(string file, string text)
    {
        if (file.Length == 0 || !File.Exists(file))
        {
            return (0, 0);
        }

        var lines = File.ReadAllLines(file);
        string[] candidates =
        [
            "\"" + text + "\"",
            "\\" + text + ".csproj\"",
            "/" + text + ".csproj\"",
            "\"" + text + ".csproj\"",
            text,
        ];
        foreach (var candidate in candidates)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var index = lines[i].IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    return (i + 1, index + 1);
                }
            }
        }

        return (0, 0);
    }

    /// <summary>Repository-relative path with forward slashes, or the full path when outside the repository.</summary>
    internal static string DisplayPath(string repositoryRoot, string path)
    {
        var root =
            Path.GetFullPath(repositoryRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(root.Length).Replace('\\', '/')
            : path;
    }

    /// <summary>Allowed values of <c>platform</c>.</summary>
    internal static class Platforms
    {
        /// <summary>No OS platform in the TFM (<c>net10.0</c>, <c>netstandard2.0</c>).</summary>
        public const string Portable = "portable";

        /// <summary>A Windows TFM (<c>net10.0-windows…</c>).</summary>
        public const string Windows = "windows";
    }

    /// <summary>Facts about the project being built.</summary>
    internal sealed class ProjectFacts(
        string name,
        string projectFile,
        string targetPlatformIdentifier,
        bool useWpf,
        bool useWindowsForms,
        string policyDisplayPath
    )
    {
        public string Name { get; } = name;

        public string ProjectFile { get; } = projectFile;

        public string TargetPlatformIdentifier { get; } = targetPlatformIdentifier;

        public bool UseWpf { get; } = useWpf;

        public bool UseWindowsForms { get; } = useWindowsForms;

        public string PolicyDisplayPath { get; } = policyDisplayPath;
    }

    /// <summary>A reference declared by a repository file.</summary>
    internal sealed class DeclaredReference(
        ReferenceKind kind,
        string name,
        bool analyzerOnly,
        string definingFile,
        string origin = ""
    )
    {
        public ReferenceKind Kind { get; } = kind;

        /// <summary>Project name, package id, framework name or assembly name.</summary>
        public string Name { get; } = name;

        public bool AnalyzerOnly { get; } = analyzerOnly;

        /// <summary>File that declares the reference.</summary>
        public string DefiningFile { get; } = definingFile;

        /// <summary>Property that implies the reference (<c>UseWPF</c>), or empty for an item.</summary>
        public string Origin { get; } = origin;
    }

    /// <summary>A policy violation, reported as an MSBuild error.</summary>
    internal sealed class Finding(string code, string message, string file, int line, int column)
    {
        public string Code { get; } = code;

        public string Message { get; } = message;

        public string File { get; } = file;

        public int Line { get; } = line;

        public int Column { get; } = column;
    }

    /// <summary>The rule of one project.</summary>
    internal sealed class ProjectRule(
        string rule,
        string platform,
        SortedSet<string> projectReferences,
        SortedSet<string> packageReferences,
        SortedSet<string> frameworkReferences,
        SortedSet<string> assemblyReferences
    )
    {
        public string Rule { get; } = rule;

        public string Platform { get; } = platform;

        public SortedSet<string> ProjectReferences { get; } = projectReferences;

        public SortedSet<string> PackageReferences { get; } = packageReferences;

        public SortedSet<string> FrameworkReferences { get; } = frameworkReferences;

        public SortedSet<string> AssemblyReferences { get; } = assemblyReferences;
    }

    /// <summary>The parsed <c>allowed-dependencies.json</c>.</summary>
    internal sealed class Policy
    {
        private Policy(
            SortedSet<string> analyzerProjects,
            SortedSet<string> globalPackages,
            Dictionary<string, ProjectRule> projects
        )
        {
            AnalyzerProjects = analyzerProjects;
            GlobalPackages = globalPackages;
            Projects = projects;
        }

        public SortedSet<string> AnalyzerProjects { get; }

        public SortedSet<string> GlobalPackages { get; }

        public Dictionary<string, ProjectRule> Projects { get; }

        /// <summary>Parses and validates the policy; throws <see cref="PolicyFormatException"/> with a position.</summary>
        public static Policy Parse(string json)
        {
            var root = JsonReader.Parse(json).Expect(JsonKind.Object, "The document");
            var analyzers = Names(
                root.Required("analyzerProjects").Expect(JsonKind.Object, "analyzerProjects"),
                "projects"
            );
            var globals = Names(
                root.Required("globalPackages").Expect(JsonKind.Object, "globalPackages"),
                "packages"
            );
            var projects = new Dictionary<string, ProjectRule>(StringComparer.OrdinalIgnoreCase);
            foreach (
                var member in root.Required("projects").Expect(JsonKind.Object, "projects").Members
            )
            {
                var node = member.Value.Expect(JsonKind.Object, "projects." + member.Key);
                node.Required("area").Expect(JsonKind.String, member.Key + ".area");
                var rule = node.Required("rule").Expect(JsonKind.String, member.Key + ".rule");
                if (rule.Text.Trim().Length == 0)
                {
                    throw new PolicyFormatException(
                        member.Key + ".rule must explain the rule.",
                        rule.Line,
                        rule.Column
                    );
                }

                var platform = node.Required("platform")
                    .Expect(JsonKind.String, member.Key + ".platform");
                if (
                    !string.Equals(platform.Text, Platforms.Portable, StringComparison.Ordinal)
                    && !string.Equals(platform.Text, Platforms.Windows, StringComparison.Ordinal)
                )
                {
                    throw new PolicyFormatException(
                        member.Key + ".platform must be \"portable\" or \"windows\".",
                        platform.Line,
                        platform.Column
                    );
                }

                var projectRule = new ProjectRule(
                    rule.Text,
                    platform.Text,
                    Names(node, "projectReferences"),
                    Names(node, "packageReferences"),
                    OptionalNames(node, "frameworkReferences"),
                    OptionalNames(node, "assemblyReferences")
                );
                if (projects.ContainsKey(member.Key))
                {
                    throw new PolicyFormatException(
                        "Project '" + member.Key + "' is declared twice (names ignore case).",
                        member.Value.Line,
                        member.Value.Column
                    );
                }

                // Dictionary.TryAdd does not exist in netstandard2.0, which RoslynCodeTaskFactory compiles against.
                projects[member.Key] = projectRule;
            }

            return new Policy(analyzers, globals, projects);
        }

        private static SortedSet<string> Names(JsonValue owner, string property) =>
            ToNames(owner.Required(property), property);

        private static SortedSet<string> OptionalNames(JsonValue owner, string property)
        {
            var value = owner.Optional(property);
            return value is null
                ? new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
                : ToNames(value, property);
        }

        private static SortedSet<string> ToNames(JsonValue array, string property)
        {
            array.Expect(JsonKind.Array, property);
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in array.Items)
            {
                item.Expect(JsonKind.String, property + " entries");
                if (!names.Add(item.Text))
                {
                    throw new PolicyFormatException(
                        property + " lists '" + item.Text + "' twice.",
                        item.Line,
                        item.Column
                    );
                }
            }

            return names;
        }
    }

    /// <summary>The policy file is not valid JSON or does not have the expected shape.</summary>
    internal sealed class PolicyFormatException(string message, int line, int column)
        : Exception(Format("{0} (line {1}, column {2})", message, line, column))
    {
        public int Line { get; } = line;

        public int Column { get; } = column;
    }

    /// <summary>JSON value kinds.</summary>
    internal enum JsonKind
    {
        /// <summary>An object.</summary>
        Object,

        /// <summary>An array.</summary>
        Array,

        /// <summary>A string.</summary>
        String,

        /// <summary>A number.</summary>
        Number,

        /// <summary><c>true</c> or <c>false</c>.</summary>
        Boolean,

        /// <summary><c>null</c>.</summary>
        Null,
    }

    /// <summary>A JSON value with its 1-based position.</summary>
    internal sealed class JsonValue(JsonKind kind, int line, int column, string text = "")
    {
        public JsonKind Kind { get; } = kind;

        public int Line { get; } = line;

        public int Column { get; } = column;

        /// <summary>String value, or the raw literal of numbers and booleans.</summary>
        public string Text { get; } = text;

        public List<JsonValue> Items { get; } = [];

        public List<KeyValuePair<string, JsonValue>> Members { get; } = [];

        public JsonValue Expect(JsonKind expected, string what) =>
            Kind == expected
                ? this
                : throw new PolicyFormatException(
                    Format("{0} must be {1} but is {2}.", what, Article(expected), Article(Kind)),
                    Line,
                    Column
                );

        public JsonValue Required(string property) =>
            Optional(property)
            ?? throw new PolicyFormatException(
                "Missing required property '" + property + "'.",
                Line,
                Column
            );

        public JsonValue? Optional(string property) =>
            Members
                .FirstOrDefault(m => string.Equals(m.Key, property, StringComparison.Ordinal))
                .Value;

        private static string Article(JsonKind kind) =>
            kind switch
            {
                JsonKind.Object => "an object",
                JsonKind.Array => "an array",
                JsonKind.String => "a string",
                JsonKind.Number => "a number",
                JsonKind.Boolean => "a boolean",
                _ => "null",
            };
    }

    /// <summary>Strict RFC 8259 reader that keeps line and column for error messages.</summary>
    internal sealed class JsonReader
    {
        private readonly string _text;
        private int _index;
        private int _line = 1;
        private int _lineStart;

        private JsonReader(string text) => _text = text;

        private int Column => _index - _lineStart + 1;

        public static JsonValue Parse(string text)
        {
            var reader = new JsonReader(text);
            if (text.Length > 0 && text[0] == '﻿')
            {
                reader._index = reader._lineStart = 1;
            }

            var value = reader.ReadValue();
            reader.SkipWhitespace();
            return reader._index == text.Length
                ? value
                : throw reader.Error("Unexpected content after the document.");
        }

        private JsonValue ReadValue()
        {
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                throw Error("Unexpected end of the document.");
            }

            var (line, column) = (_line, Column);
            return _text[_index] switch
            {
                '{' => ReadObject(),
                '[' => ReadArray(),
                '"' => new JsonValue(JsonKind.String, line, column, ReadString()),
                't' => ReadLiteral("true", JsonKind.Boolean),
                'f' => ReadLiteral("false", JsonKind.Boolean),
                'n' => ReadLiteral("null", JsonKind.Null),
                _ => ReadNumber(),
            };
        }

        private JsonValue ReadObject()
        {
            var value = new JsonValue(JsonKind.Object, _line, Column);
            _index++;
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return value;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != '"')
                {
                    throw Error("Expected a property name.");
                }

                var (line, column) = (_line, Column);
                var key = ReadString();
                if (!keys.Add(key))
                {
                    throw new PolicyFormatException(
                        "Duplicate property '" + key + "'.",
                        line,
                        column
                    );
                }

                SkipWhitespace();
                if (!TryConsume(':'))
                {
                    throw Error("Expected ':' after a property name.");
                }

                value.Members.Add(new KeyValuePair<string, JsonValue>(key, ReadValue()));
                SkipWhitespace();
            } while (TryConsume(','));

            return TryConsume('}') ? value : throw Error("Expected ',' or '}'.");
        }

        private JsonValue ReadArray()
        {
            var value = new JsonValue(JsonKind.Array, _line, Column);
            _index++;
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return value;
            }

            do
            {
                value.Items.Add(ReadValue());
                SkipWhitespace();
            } while (TryConsume(','));

            return TryConsume(']') ? value : throw Error("Expected ',' or ']'.");
        }

        private string ReadString()
        {
            _index++;
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var c = _text[_index];
                if (c == '"')
                {
                    _index++;
                    return builder.ToString();
                }

                if (c < ' ')
                {
                    throw Error("Control characters must be escaped inside strings.");
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    _index++;
                    continue;
                }

                builder.Append(ReadEscape());
            }

            throw Error("Unterminated string.");
        }

        private char ReadEscape()
        {
            var escape = _index + 1 < _text.Length ? _text[_index + 1] : '\0';
            var unescaped = escape switch
            {
                '"' => '"',
                '\\' => '\\',
                '/' => '/',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => '\0',
            };
            if (unescaped != '\0')
            {
                _index += 2;
                return unescaped;
            }

            var code = 0;
            for (var i = 0; escape == 'u' && i < 4; i++)
            {
                var digit = _index + 2 + i < _text.Length ? HexValue(_text[_index + 2 + i]) : -1;
                if (digit < 0)
                {
                    break;
                }

                code = (code * 16) + digit;
                if (i == 3)
                {
                    _index += 6;
                    return (char)code;
                }
            }

            throw Error("Invalid escape sequence.");
        }

        private static int HexValue(char c) =>
            c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };

        private JsonValue ReadLiteral(string literal, JsonKind kind)
        {
            if (string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
            {
                throw Error("Invalid literal.");
            }

            var value = new JsonValue(kind, _line, Column, literal);
            _index += literal.Length;
            return value;
        }

        private JsonValue ReadNumber()
        {
            var (start, line, column) = (_index, _line, Column);
            TryConsume('-');
            if (!TryConsume('0') && !ConsumeDigits())
            {
                throw Error("Invalid value.");
            }

            if (TryConsume('.') && !ConsumeDigits())
            {
                throw Error("Expected digits after the decimal point.");
            }

            if (TryConsume('e') || TryConsume('E'))
            {
                _ = TryConsume('+') || TryConsume('-');
                if (!ConsumeDigits())
                {
                    throw Error("Expected digits in the exponent.");
                }
            }

            return new JsonValue(
                JsonKind.Number,
                line,
                column,
                _text.Substring(start, _index - start)
            );
        }

        private bool ConsumeDigits()
        {
            var start = _index;
            while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
            {
                _index++;
            }

            return _index > start;
        }

        private bool TryConsume(char expected)
        {
            if (_index < _text.Length && _text[_index] == expected)
            {
                _index++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            for (; _index < _text.Length; _index++)
            {
                var c = _text[_index];
                if (c == '\n')
                {
                    _line++;
                    _lineStart = _index + 1;
                }
                else if (c != ' ' && c != '\t' && c != '\r')
                {
                    return;
                }
            }
        }

        private PolicyFormatException Error(string message) => new(message, _line, Column);
    }
}
