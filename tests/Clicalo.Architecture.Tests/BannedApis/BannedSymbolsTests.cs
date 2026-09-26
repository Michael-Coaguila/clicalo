using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>
/// The BannedSymbols lists (blueprint §4.4, mechanism 3) are resolved with Roslyn exactly as
/// Microsoft.CodeAnalysis.BannedApiAnalyzers does: a typo or an unlisted overload would silently ban nothing, so every
/// entry must resolve and every banned family must list all its overloads, including the ones a new .NET adds.
/// </summary>
public sealed class BannedSymbolsTests
{
    private static readonly Lazy<CSharpCompilation> Runtime = new(() =>
        CSharpCompilation.Create(
            "BannedSymbolsProbe",
            references: ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => MetadataReference.CreateFromFile(path))
        )
    );

    public static TheoryData<string> Lists =>
        ["All", "Domain", "Application", "Presentation", "Surfaces"];

    [Theory]
    [MemberData(nameof(Lists))]
    public void Every_entry_is_a_documentation_id_with_a_reason(string list)
    {
        var entries = Entries(list);

        entries.ShouldNotBeEmpty();
        foreach (var (id, reason) in entries)
        {
            Regex
                .IsMatch(
                    id,
                    "^[TMPFEN]:[A-Za-z_][^\\s;]*$",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1)
                )
                .ShouldBeTrue(id);
            reason.Length.ShouldBeGreaterThan(10, id);
        }
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void Every_entry_resolves_to_a_symbol(string list)
    {
        var unresolved = Entries(list)
            .Select(entry => entry.Id)
            .Where(id => !IsGeneratedOrExternal(id))
            .Where(id =>
                DocumentationCommentId.GetSymbolsForDeclarationId(id, Runtime.Value).IsEmpty
            )
            .ToList();

        unresolved.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public void No_entry_is_listed_twice_for_the_same_project(string list)
    {
        var ids = Entries(list).Select(entry => entry.Id).ToList();
        if (!string.Equals(list, "All", StringComparison.Ordinal))
        {
            ids.AddRange(Entries("All").Select(entry => entry.Id));
        }

        ids.ShouldBeUnique(StringComparer.Ordinal);
    }

    [Fact]
    public void Every_CsWin32_entry_is_exercised_by_the_build_test()
    {
        var functions = BannedApiProbes.NativeMethods.Split(
            '\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

        foreach (
            var id in Entries("All")
                .Select(entry => entry.Id)
                .Where(id => id.StartsWith("M:Windows.Win32.PInvoke.", StringComparison.Ordinal))
        )
        {
            var function = id["M:Windows.Win32.PInvoke.".Length..].Split('(')[0];
            functions.ShouldContain(f => string.Equals(f, function, StringComparison.Ordinal), id);
        }
    }

    [Fact]
    [Trait("Req", "NFR-013")]
    [Trait("Req", "SEG-007")]
    public void Every_overload_of_a_banned_family_is_banned()
    {
        var banned = Entries("All").Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);

        var missing = Families().Where(id => !banned.Contains(id)).ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Every_list_is_wired_by_Directory_Build_targets_for_its_layer()
    {
        var targets = File.ReadAllText(RepoPaths.Combine("Directory.Build.targets"));

        foreach (var list in new[] { "All", "Domain", "Application", "Presentation", "Surfaces" })
        {
            targets.ShouldContain("BannedSymbols." + list + ".txt", Case.Sensitive);
        }

        targets.ShouldContain("BannedSymbols.globalconfig", Case.Sensitive);
        File.ReadAllText(ArchitectureDocuments.PathOf("BannedSymbols.globalconfig"))
            .ShouldContain(
                "dotnet_banned_api_analyzer.exclude_generated_code = true",
                Case.Sensitive
            );
    }

    /// <summary>Entries of <c>architecture/BannedSymbols.{list}.txt</c>, without comments and blank lines.</summary>
    internal static ImmutableArray<(string Id, string Reason)> Entries(string list) =>
        [
            .. File.ReadAllLines(ArchitectureDocuments.PathOf("BannedSymbols." + list + ".txt"))
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal))
                .Select(line =>
                {
                    var separator = line.IndexOf(';', StringComparison.Ordinal);
                    return separator < 0
                        ? (line, string.Empty)
                        : (line[..separator].Trim(), line[(separator + 1)..].Trim());
                }),
        ];

    /// <summary>
    /// CsWin32 interop is generated per project (the Surfaces build test proves those ids), and System.Management is a
    /// package no project references today.
    /// </summary>
    private static bool IsGeneratedOrExternal(string id) =>
        id[2..].StartsWith("Windows.Win32.", StringComparison.Ordinal)
        || string.Equals(id, "N:System.Management", StringComparison.Ordinal);

    private static IEnumerable<string> Families() =>
        Members("System.DateTime", m => m.Name is "Now" or "UtcNow" or "Today")
            .Concat(Members("System.DateTimeOffset", m => m.Name is "Now" or "UtcNow"))
            .Concat(
                Members("System.Environment", m => m.Name is "TickCount" or "TickCount64" or "Exit")
            )
            .Concat(
                Members(
                    "System.Diagnostics.Stopwatch",
                    m =>
                        m.Name is "StartNew" or "GetTimestamp"
                        || IsConstructor(m)
                        || m is IMethodSymbol { Name: "GetElapsedTime", Parameters.Length: 1 }
                )
            )
            .Concat(
                Members(
                    "System.Threading.Tasks.Task",
                    m =>
                        (m.Name is "Delay" or "WaitAsync" && TakesClockTime(m))
                        || m.Name is "Wait" or "WaitAll" or "WaitAny"
                )
            )
            .Concat(
                Members(
                    "System.Threading.Tasks.Task`1",
                    m => (m.Name is "WaitAsync" && TakesClockTime(m)) || m.Name is "Result"
                )
            )
            .Concat(Members("System.Threading.Tasks.ValueTask`1", m => m.Name is "Result"))
            .Concat(Members("System.Threading.Thread", m => m.Name is "Sleep"))
            .Concat(
                Members(
                    "System.Threading.CancellationTokenSource",
                    m => IsConstructor(m) && TakesClockTime(m)
                )
            )
            .Concat(
                Members(
                    "System.Threading.PeriodicTimer",
                    m => IsConstructor(m) && TakesClockTime(m)
                )
            )
            .Concat(Members("System.Guid", m => m.Name is "NewGuid" or "CreateVersion7"))
            .Concat(
                Members(
                    "System.Random",
                    m =>
                        m.Name is "Shared"
                        || m
                            is IMethodSymbol
                            {
                                MethodKind: MethodKind.Constructor,
                                Parameters.Length: 0
                            }
                )
            )
            .Concat(Members("System.Diagnostics.Process", m => m.Name is "Start"))
            .Concat(Members("System.IO.File", m => IsFileWrite(m.Name)))
            .Concat(
                Members(
                    "System.IO.FileInfo",
                    m => m.Name is "Create" or "CreateText" or "AppendText" or "Open" or "OpenWrite"
                )
            )
            .Concat(Members("System.IO.FileStream", IsConstructor))
            .Concat(
                Members(
                    "System.IO.StreamWriter",
                    m =>
                        m
                            is IMethodSymbol
                            {
                                MethodKind: MethodKind.Constructor,
                                Parameters: [{ Type.SpecialType: SpecialType.System_String }, ..]
                            }
                )
            )
            .Concat(Members("System.Runtime.CompilerServices.TaskAwaiter", IsGetResult))
            .Concat(Members("System.Runtime.CompilerServices.TaskAwaiter`1", IsGetResult))
            .Concat(Members("System.Runtime.CompilerServices.ValueTaskAwaiter", IsGetResult))
            .Concat(Members("System.Runtime.CompilerServices.ValueTaskAwaiter`1", IsGetResult))
            .Concat(
                Members(
                    "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter",
                    IsGetResult
                )
            )
            .Concat(
                Members(
                    "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter",
                    IsGetResult
                )
            )
            .Concat(
                Members(
                    "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter",
                    IsGetResult
                )
            )
            .Concat(
                Members(
                    "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1+ConfiguredValueTaskAwaiter",
                    IsGetResult
                )
            )
            .Concat(Members("System.Windows.Application", m => m.Name is "Shutdown"));

    private static List<string> Members(string metadataName, Func<ISymbol, bool> select)
    {
        var type =
            Runtime
                .Value.GetTypesByMetadataName(metadataName)
                .SingleOrDefault(t =>
                    t.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
                )
            ?? throw new InvalidOperationException(
                "Type not found in the runtime: " + metadataName
            );
        var members = type.GetMembers()
            .Where(m =>
                m.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
                && m
                    is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor }
                        or IPropertySymbol
            )
            .Where(select)
            .Select(m => DocumentationCommentId.CreateDeclarationId(m)!.Split('~')[0])
            .ToList();
        members.ShouldNotBeEmpty(metadataName);
        return members;
    }

    private static bool IsConstructor(ISymbol member) =>
        member is IMethodSymbol { MethodKind: MethodKind.Constructor };

    private static bool IsGetResult(ISymbol member) => member.Name is "GetResult";

    /// <summary>Overloads that wait on the system clock: a TimeSpan or milliseconds, without a TimeProvider.</summary>
    private static bool TakesClockTime(ISymbol member) =>
        member is IMethodSymbol method
        && method.Parameters.Any(p =>
            p.Type.SpecialType == SpecialType.System_Int32
            || string.Equals(p.Type.Name, "TimeSpan", StringComparison.Ordinal)
        )
        && method.Parameters.All(p =>
            !string.Equals(p.Type.Name, "TimeProvider", StringComparison.Ordinal)
        );

    private static bool IsFileWrite(string name) =>
        (
            name.StartsWith("Write", StringComparison.Ordinal)
            || name.StartsWith("Append", StringComparison.Ordinal)
            || name is "Create" or "CreateText" or "Open" or "OpenWrite" or "OpenHandle"
        );
}
