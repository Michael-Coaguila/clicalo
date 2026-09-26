// Reference whitelist check (blueprint §4.4, mechanism 1).
//
// This single file is the source of the ClicaloVerifyReferences MSBuild task. RoslynCodeTaskFactory compiles it on
// demand for the MSBuild that runs the build (.NET in the CLI, .NET Framework in Visual Studio), so it only uses the
// netstandard2.0 surface: no records, no init accessors and no System.Text.Json. Clicalo.Architecture.Tests links the
// same file with CLICALO_REFERENCE_POLICY_TESTS defined to unit test the policy under the repository analyzers; that
// symbol removes the thin MSBuild adapter and keeps the pure policy.
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
#if !CLICALO_REFERENCE_POLICY_TESTS
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

    private const string WpfFramework = "Microsoft.WindowsDesktop.App.WPF";
    private const string WindowsFormsFramework = "Microsoft.WindowsDesktop.App.WindowsForms";
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

    /// <summary><c>$(TargetPlatformIdentifier)</c>: empty for portable frameworks, <c>windows</c> for Windows ones.</summary>
    public string TargetPlatformIdentifier { get; set; } = string.Empty;

    /// <summary><c>$(UseWPF)</c>, which adds the WPF framework reference.</summary>
    public bool UseWpf { get; set; }

    /// <summary><c>$(UseWindowsForms)</c>, which adds the Windows Forms framework reference.</summary>
    public bool UseWindowsForms { get; set; }

    /// <summary>
    /// References declared by repository files, with <c>ClicaloKind</c> metadata
    /// (Project, Package, GlobalPackage, Framework or Assembly).
    /// </summary>
    public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();

    /// <inheritdoc />
    public override bool Execute()
    {
        var policyPath = Path.GetFullPath(PolicyFile);
        var project = new ProjectFacts(
            ProjectName,
            ProjectFile,
            TargetPlatformIdentifier,
            UseWpf,
            UseWindowsForms,
            DisplayPath(RepositoryRoot, policyPath)
        );

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

        var references = new List<DeclaredReference>(References.Length);
        foreach (var item in References)
        {
            references.Add(
                new DeclaredReference(
                    ParseKind(item.GetMetadata("ClicaloKind")),
                    ReferenceName(item),
                    IsAnalyzerOnly(
                        item.GetMetadata("OutputItemType"),
                        item.GetMetadata("ReferenceOutputAssembly")
                    ),
                    item.GetMetadata("ClicaloDefiningFile")
                )
            );
        }

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

    private static string ReferenceName(ITaskItem item)
    {
        var kind = ParseKind(item.GetMetadata("ClicaloKind"));
        if (kind == ReferenceKind.Project)
        {
            return item.GetMetadata("Filename");
        }

        return kind == ReferenceKind.Assembly ? AssemblyName(item.ItemSpec) : item.ItemSpec;
    }
#endif

    /// <summary>
    /// Simple name of an assembly <c>Reference</c>: <c>Foo</c> for <c>Foo, Version=1.0</c> and for <c>lib\Foo.dll</c>.
    /// </summary>
    internal static string AssemblyName(string itemSpec)
    {
        if (itemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(itemSpec);
        }

        var comma = itemSpec.IndexOf(',');
        return (comma < 0 ? itemSpec : itemSpec.Substring(0, comma)).Trim();
    }

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

    /// <summary>Maps the <c>ClicaloKind</c> metadata to a <see cref="ReferenceKind"/>.</summary>
    internal static ReferenceKind ParseKind(string value)
    {
        foreach (ReferenceKind kind in Enum.GetValues(typeof(ReferenceKind)))
        {
            if (string.Equals(kind.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new ArgumentException("Unknown reference kind '" + value + "'.", nameof(value));
    }

    /// <summary>
    /// True for a reference that only loads a Roslyn component into the compiler
    /// (<c>OutputItemType="Analyzer"</c> and <c>ReferenceOutputAssembly="false"</c>).
    /// </summary>
    internal static bool IsAnalyzerOnly(string outputItemType, string referenceOutputAssembly) =>
        string.Equals(outputItemType, "Analyzer", StringComparison.OrdinalIgnoreCase)
        && string.Equals(referenceOutputAssembly, "false", StringComparison.OrdinalIgnoreCase);

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
            findings.Add(
                new Finding(
                    UndeclaredProjectCode,
                    "Project '"
                        + project.Name
                        + "' is not declared in "
                        + project.PolicyDisplayPath
                        + ". Every project declares its area, rule, platform and allowed references there; see "
                        + EnforcementGuide
                        + ".",
                    project.ProjectFile,
                    0,
                    0
                )
            );
            return findings;
        }

        CheckPlatform(rule, project, findings);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in ImplicitFrameworks(project).Concat(references))
        {
            if (!seen.Add(reference.Kind + "|" + reference.Name + "|" + reference.AnalyzerOnly))
            {
                continue;
            }

            if (!IsAllowed(policy, rule, reference))
            {
                findings.Add(Forbidden(policy, rule, project, reference));
            }
        }

        return findings;
    }

    private static IEnumerable<DeclaredReference> ImplicitFrameworks(ProjectFacts project)
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

    private static void CheckPlatform(
        ProjectRule rule,
        ProjectFacts project,
        List<Finding> findings
    )
    {
        var isWindows = string.Equals(
            project.TargetPlatformIdentifier,
            "windows",
            StringComparison.OrdinalIgnoreCase
        );
        var isPortable = project.TargetPlatformIdentifier.Length == 0;
        var ok = rule.Platform switch
        {
            Platforms.Portable => isPortable,
            Platforms.Windows => isWindows,
            _ => true,
        };
        if (ok)
        {
            return;
        }

        var actual = isPortable
            ? "a portable framework"
            : "platform '" + project.TargetPlatformIdentifier + "'";
        var (line, column) = Locate(project.ProjectFile, "<TargetFramework");
        findings.Add(
            new Finding(
                WrongPlatformCode,
                "Project '"
                    + project.Name
                    + "' must target "
                    + (
                        rule.Platform == Platforms.Portable
                            ? "a portable framework (no OS platform)"
                            : "Windows"
                    )
                    + " but targets "
                    + actual
                    + ". Rule for "
                    + project.Name
                    + ": "
                    + rule.Rule
                    + " See "
                    + EnforcementGuide
                    + ".",
                project.ProjectFile,
                line,
                column
            )
        );
    }

    private static bool IsAllowed(Policy policy, ProjectRule rule, DeclaredReference reference) =>
        reference.Kind switch
        {
            ReferenceKind.Project => rule.ProjectReferences.Contains(reference.Name)
                || reference.AnalyzerOnly && policy.AnalyzerProjects.Contains(reference.Name),
            ReferenceKind.Package => rule.PackageReferences.Contains(reference.Name)
                || policy.GlobalPackages.Contains(reference.Name),
            ReferenceKind.GlobalPackage => policy.GlobalPackages.Contains(reference.Name),
            ReferenceKind.Framework => rule.FrameworkReferences.Contains(reference.Name),
            ReferenceKind.Assembly => rule.AssemblyReferences.Contains(reference.Name),
            _ => false,
        };

    private static Finding Forbidden(
        Policy policy,
        ProjectRule rule,
        ProjectFacts project,
        DeclaredReference reference
    )
    {
        var (what, allowedLabel, allowed) = reference.Kind switch
        {
            ReferenceKind.Project => (
                reference.AnalyzerOnly ? "Analyzer project reference" : "Project reference",
                "project references",
                reference.AnalyzerOnly
                    ? Union(rule.ProjectReferences, policy.AnalyzerProjects)
                    : rule.ProjectReferences
            ),
            ReferenceKind.Package => (
                "Package reference",
                "package references",
                rule.PackageReferences
            ),
            ReferenceKind.GlobalPackage => (
                "Global package reference",
                "global packages",
                policy.GlobalPackages
            ),
            ReferenceKind.Framework => (
                "Framework reference",
                "framework references",
                rule.FrameworkReferences
            ),
            _ => ("Assembly reference", "assembly references", rule.AssemblyReferences),
        };

        var message = new StringBuilder()
            .Append(what)
            .Append(" '")
            .Append(reference.Name)
            .Append('\'')
            .Append(
                reference.Origin.Length == 0 ? string.Empty : " (" + reference.Origin + "=true)"
            )
            .Append(" is not allowed in '")
            .Append(project.Name)
            .Append("'. ")
            .Append(project.PolicyDisplayPath)
            .Append(" allows ")
            .Append(allowedLabel)
            .Append(": ")
            .Append(Describe(allowed))
            .Append(". Rule for ")
            .Append(project.Name)
            .Append(": ")
            .Append(rule.Rule);
        if (reference.Kind == ReferenceKind.Assembly)
        {
            message.Append(" Prefer a PackageReference or a ProjectReference.");
        }

        message
            .Append(" If the dependency is intended, change the whitelist with a justification (")
            .Append(EnforcementGuide)
            .Append(").");

        var file =
            reference.DefiningFile.Length == 0 ? project.ProjectFile : reference.DefiningFile;
        var (line, column) = Locate(
            file,
            reference.Origin.Length == 0 ? reference.Name : "<" + reference.Origin
        );
        return new Finding(ForbiddenReferenceCode, message.ToString(), file, line, column);
    }

    private static SortedSet<string> Union(SortedSet<string> first, SortedSet<string> second)
    {
        var union = new SortedSet<string>(first, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(second);
        return union;
    }

    private static string Describe(SortedSet<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);

    /// <summary>
    /// Best-effort position (1-based) of <paramref name="text"/> in <paramref name="file"/>, so IDEs jump to the line
    /// that declares the reference. Returns (0, 0) when the file or the text cannot be found.
    /// </summary>
    internal static (int Line, int Column) Locate(string file, string text)
    {
        if (file.Length == 0 || !File.Exists(file))
        {
            return (0, 0);
        }

        var lines = File.ReadAllLines(file);
        var candidates = new[]
        {
            "\"" + text + "\"",
            "\\" + text + ".csproj\"",
            "/" + text + ".csproj\"",
            "\"" + text + ".csproj\"",
            text,
        };
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
        var root = Path.GetFullPath(repositoryRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            root += Path.DirectorySeparatorChar;
        }

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
    internal sealed class ProjectFacts
    {
        public ProjectFacts(
            string name,
            string projectFile,
            string targetPlatformIdentifier,
            bool useWpf,
            bool useWindowsForms,
            string policyDisplayPath
        )
        {
            Name = name;
            ProjectFile = projectFile;
            TargetPlatformIdentifier = targetPlatformIdentifier;
            UseWpf = useWpf;
            UseWindowsForms = useWindowsForms;
            PolicyDisplayPath = policyDisplayPath;
        }

        public string Name { get; }

        public string ProjectFile { get; }

        public string TargetPlatformIdentifier { get; }

        public bool UseWpf { get; }

        public bool UseWindowsForms { get; }

        public string PolicyDisplayPath { get; }
    }

    /// <summary>A reference declared by a repository file.</summary>
    internal sealed class DeclaredReference
    {
        public DeclaredReference(
            ReferenceKind kind,
            string name,
            bool analyzerOnly,
            string definingFile,
            string origin = ""
        )
        {
            Kind = kind;
            Name = name;
            AnalyzerOnly = analyzerOnly;
            DefiningFile = definingFile;
            Origin = origin;
        }

        public ReferenceKind Kind { get; }

        /// <summary>Project name, package id, framework name or assembly name.</summary>
        public string Name { get; }

        public bool AnalyzerOnly { get; }

        /// <summary>File that declares the reference (<c>DefiningProjectFullPath</c>).</summary>
        public string DefiningFile { get; }

        /// <summary>Property that implies the reference (<c>UseWPF</c>), or empty for an item.</summary>
        public string Origin { get; }
    }

    /// <summary>A policy violation, reported as an MSBuild error.</summary>
    internal sealed class Finding
    {
        public Finding(string code, string message, string file, int line, int column)
        {
            Code = code;
            Message = message;
            File = file;
            Line = line;
            Column = column;
        }

        public string Code { get; }

        public string Message { get; }

        public string File { get; }

        public int Line { get; }

        public int Column { get; }
    }

    /// <summary>The rule of one project.</summary>
    internal sealed class ProjectRule
    {
        public ProjectRule(
            string name,
            string rule,
            string platform,
            SortedSet<string> projectReferences,
            SortedSet<string> packageReferences,
            SortedSet<string> frameworkReferences,
            SortedSet<string> assemblyReferences
        )
        {
            Name = name;
            Rule = rule;
            Platform = platform;
            ProjectReferences = projectReferences;
            PackageReferences = packageReferences;
            FrameworkReferences = frameworkReferences;
            AssemblyReferences = assemblyReferences;
        }

        public string Name { get; }

        public string Rule { get; }

        public string Platform { get; }

        public SortedSet<string> ProjectReferences { get; }

        public SortedSet<string> PackageReferences { get; }

        public SortedSet<string> FrameworkReferences { get; }

        public SortedSet<string> AssemblyReferences { get; }
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
            var root = JsonReader.Parse(json);
            root.Expect(JsonKind.Object, "the document");

            var analyzers = Names(
                root.Required("analyzerProjects").Expect(JsonKind.Object, "analyzerProjects"),
                "projects"
            );
            var globals = Names(
                root.Required("globalPackages").Expect(JsonKind.Object, "globalPackages"),
                "packages"
            );

            var projectsNode = root.Required("projects").Expect(JsonKind.Object, "projects");
            var projects = new Dictionary<string, ProjectRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in projectsNode.Members)
            {
                var node = member.Value.Expect(JsonKind.Object, "projects." + member.Key);
                var platform = node.Required("platform")
                    .Expect(JsonKind.String, member.Key + ".platform");
                if (platform.Text != Platforms.Portable && platform.Text != Platforms.Windows)
                {
                    throw new PolicyFormatException(
                        member.Key + ".platform must be \"portable\" or \"windows\".",
                        platform.Line,
                        platform.Column
                    );
                }

                var rule = node.Required("rule").Expect(JsonKind.String, member.Key + ".rule");
                if (rule.Text.Trim().Length == 0)
                {
                    throw new PolicyFormatException(
                        member.Key + ".rule must explain the rule.",
                        rule.Line,
                        rule.Column
                    );
                }

                node.Required("area").Expect(JsonKind.String, member.Key + ".area");
                projects.Add(
                    member.Key,
                    new ProjectRule(
                        member.Key,
                        rule.Text,
                        platform.Text,
                        Names(node, "projectReferences"),
                        Names(node, "packageReferences"),
                        OptionalNames(node, "frameworkReferences"),
                        OptionalNames(node, "assemblyReferences")
                    )
                );
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
    internal sealed class PolicyFormatException : Exception
    {
        public PolicyFormatException(string message, int line, int column)
            : base(
                message
                    + " (line "
                    + line.ToString(CultureInfo.InvariantCulture)
                    + ", column "
                    + column.ToString(CultureInfo.InvariantCulture)
                    + ")"
            )
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }
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
    internal sealed class JsonValue
    {
        public JsonValue(JsonKind kind, int line, int column)
        {
            Kind = kind;
            Line = line;
            Column = column;
        }

        public JsonKind Kind { get; }

        public int Line { get; }

        public int Column { get; }

        /// <summary>String value, or the raw literal of numbers and booleans.</summary>
        public string Text { get; set; } = string.Empty;

        public List<JsonValue> Items { get; } = new List<JsonValue>();

        public List<KeyValuePair<string, JsonValue>> Members { get; } =
            new List<KeyValuePair<string, JsonValue>>();

        public JsonValue Expect(JsonKind kind, string what)
        {
            if (Kind != kind)
            {
                throw new PolicyFormatException(
                    what + " must be " + Article(kind) + " but is " + Article(Kind) + ".",
                    Line,
                    Column
                );
            }

            return this;
        }

        public JsonValue Required(string property) =>
            Optional(property)
            ?? throw new PolicyFormatException(
                "Missing required property '" + property + "'.",
                Line,
                Column
            );

        public JsonValue? Optional(string property)
        {
            foreach (var member in Members)
            {
                if (string.Equals(member.Key, property, StringComparison.Ordinal))
                {
                    return member.Value;
                }
            }

            return null;
        }

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

        public static JsonValue Parse(string text)
        {
            var reader = new JsonReader(text);
            reader.SkipWhitespace();
            if (reader._index < text.Length && text[reader._index] == '﻿')
            {
                reader._index++;
                reader._lineStart = reader._index;
            }

            var value = reader.ReadValue();
            reader.SkipWhitespace();
            if (reader._index != text.Length)
            {
                throw reader.Error("Unexpected content after the JSON document.");
            }

            return value;
        }

        private int Column => _index - _lineStart + 1;

        private JsonValue ReadValue()
        {
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                throw Error("Unexpected end of the JSON document.");
            }

            switch (_text[_index])
            {
                case '{':
                    return ReadObject();
                case '[':
                    return ReadArray();
                case '"':
                    var line = _line;
                    var column = Column;
                    return new JsonValue(JsonKind.String, line, column) { Text = ReadString() };
                case 't':
                    return ReadLiteral("true", JsonKind.Boolean);
                case 'f':
                    return ReadLiteral("false", JsonKind.Boolean);
                case 'n':
                    return ReadLiteral("null", JsonKind.Null);
                default:
                    return ReadNumber();
            }
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
            while (true)
            {
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != '"')
                {
                    throw Error("Expected a property name.");
                }

                var keyLine = _line;
                var keyColumn = Column;
                var key = ReadString();
                if (!keys.Add(key))
                {
                    throw new PolicyFormatException(
                        "Duplicate property '" + key + "'.",
                        keyLine,
                        keyColumn
                    );
                }

                SkipWhitespace();
                if (!TryConsume(':'))
                {
                    throw Error("Expected ':' after a property name.");
                }

                value.Members.Add(new KeyValuePair<string, JsonValue>(key, ReadValue()));
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return value;
                }

                if (!TryConsume(','))
                {
                    throw Error("Expected ',' or '}'.");
                }
            }
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

            while (true)
            {
                value.Items.Add(ReadValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return value;
                }

                if (!TryConsume(','))
                {
                    throw Error("Expected ',' or ']'.");
                }
            }
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

                if (_index + 1 >= _text.Length)
                {
                    break;
                }

                var escape = _text[_index + 1];
                _index += 2;
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escape);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u'
                        when _index + 4 <= _text.Length
                            && int.TryParse(
                                _text.Substring(_index, 4),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out var code
                            ):
                        builder.Append((char)code);
                        _index += 4;
                        break;
                    default:
                        _index -= 2;
                        throw Error("Invalid escape sequence.");
                }
            }

            throw Error("Unterminated string.");
        }

        private JsonValue ReadLiteral(string literal, JsonKind kind)
        {
            if (string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
            {
                throw Error("Invalid literal.");
            }

            var value = new JsonValue(kind, _line, Column) { Text = literal };
            _index += literal.Length;
            return value;
        }

        private JsonValue ReadNumber()
        {
            var start = _index;
            var value = new JsonValue(JsonKind.Number, _line, Column);
            TryConsume('-');
            if (TryConsume('0'))
            {
                // A leading zero cannot be followed by more digits.
            }
            else if (!ConsumeDigits())
            {
                throw Error("Invalid value.");
            }

            if (TryConsume('.') && !ConsumeDigits())
            {
                throw Error("Expected digits after the decimal point.");
            }

            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                _index++;
                if (!TryConsume('+'))
                {
                    TryConsume('-');
                }

                if (!ConsumeDigits())
                {
                    throw Error("Expected digits in the exponent.");
                }
            }

            value.Text = _text.Substring(start, _index - start);
            return value;
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
            while (_index < _text.Length)
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

                _index++;
            }
        }

        private PolicyFormatException Error(string message) =>
            new PolicyFormatException(message, _line, Column);
    }
}
