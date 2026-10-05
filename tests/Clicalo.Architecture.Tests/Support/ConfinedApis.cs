using System.Collections.Immutable;
using ArchUnitNET.Domain;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// The IL-level counterpart of the banned-API table (blueprint §4.4, mechanism 3). The BannedSymbols lists stop a
/// banned call at compile time; these rules prove in the compiled product that each API is only reached from the
/// namespace or type the table allows, whatever the overload, suppression or generated code involved.
/// </summary>
internal static class ConfinedApis
{
    /// <summary>
    /// One rule per row of the table, in <paramref name="scope"/>: the product, or a fixture area where the CsWin32
    /// interop namespace and the allowed namespaces are mirrored for the negative tests. BCL APIs are never mapped.
    /// </summary>
    public static ImmutableArray<ConfinedApi> Catalog(Scope scope)
    {
        var interop = scope.Name("Windows.Win32");
        var pinvoke = interop + ".PInvoke";
        Zone Allowed(params string[] names) =>
            names.Length == 0
                ? Zone.Nothing
                : Zone.AnyOf(string.Join(" or ", names), [.. names.Select(scope.NamespaceOrType)]);

        return
        [
            new(
                "SetForegroundWindow",
                Calls(pinvoke, "SetForegroundWindow", "AllowSetForegroundWindow"),
                Uses(),
                Allowed("Clicalo.Platform.Windows.Foreground.ForegroundControl"),
                "every foreground change goes through ForegroundOrchestrator leases (§3.6, REG-01)"
            ),
            new(
                "AttachThreadInput",
                Calls(pinvoke, "AttachThreadInput", "LockSetForegroundWindow"),
                Uses(),
                Allowed(),
                "AttachThreadInput and LockSetForegroundWindow are forbidden everywhere (§3.6)"
            ),
            new(
                "TrackPopupMenu",
                Calls(pinvoke, "TrackPopupMenu", "TrackPopupMenuEx"),
                Uses(),
                Allowed("Clicalo.Platform.Windows.Tray.TrayMenuHost"),
                "the tray menu only opens inside the TrayMenu lease (§3.6)"
            ),
            new(
                "SendInput",
                Calls(pinvoke, "SendInput"),
                Uses(),
                Allowed("Clicalo.Platform.Core.Injection"),
                "every injection goes through the single SendInput of Platform.Core (ADR-0022, SEG-007)"
            ),
            new(
                "ShellExecute",
                Calls(pinvoke, "ShellExecute", "ShellExecuteEx"),
                Uses(
                    interop + ".UI.Shell.IShellDispatch2",
                    interop + ".UI.Shell.SHELLEXECUTEINFOW",
                    "System.Management"
                ),
                Allowed(
                    "Clicalo.Platform.Windows.Launch",
                    "Clicalo.Platform.Windows.SystemCommands",
                    "Clicalo.Platform.Windows.Elevation"
                ),
                "the shell and WMI run behind ILauncher and ISystemCommandRunner on the Shell thread (§3.1, §3.3)"
            ),
            new(
                "Process.Start",
                Calls("System.Diagnostics.Process", "Start"),
                Uses("System.Diagnostics.ProcessStartInfo"),
                Allowed("Clicalo.Platform.Windows.Launch"),
                "apps start through ILauncher, never through a command interpreter (LOG-008, NFR-009)"
            ),
            new(
                "file writes",
                FileWrites,
                Uses(),
                Allowed(
                    "Clicalo.Infrastructure.Persistence.AtomicFile",
                    "Clicalo.Infrastructure.Logging.FixedNameRollingFileSink"
                ),
                "files are written through IAtomicFileWriter (ADR-0007)"
            ),
            new(
                "process exit",
                Either(
                    Calls("System.Environment", "Exit"),
                    Calls("System.Windows.Application", "Shutdown")
                ),
                Uses(),
                Allowed("Clicalo.App.Lifecycle"),
                "the process ends through IAppLifetime.ExitAsync, which releases every key first (SEG-007)"
            ),
            new(
                "blocking waits",
                Either(
                    Calls("System.Threading.Tasks.Task", "Wait", "WaitAll", "WaitAny"),
                    Calls("System.Threading.Tasks.Task`1", "get_Result"),
                    Calls("System.Threading.Tasks.ValueTask`1", "get_Result")
                ),
                Uses(),
                Allowed("Clicalo.App.Shutdown"),
                "code awaits; only the last synchronous flush on exit may block (§4.4)"
            ),
            new(
                "Console",
                Nothing,
                Uses("System.Console"),
                Allowed(),
                "diagnostics go through ILogger (§9.4)"
            ),
            new(
                "window activation",
                Either(
                    Calls("System.Windows.Window", "Activate"),
                    Calls("System.Windows.UIElement", "Focus")
                ),
                Uses(),
                Allowed(),
                "windows are never activated directly: ControlCenter, TextInput and KeyboardNavigation leases (§3.6, REG-01)"
            ),
            new(
                "clock, timers, ids and randomness",
                ClockAndIdentity,
                Uses("System.Threading.Timer", "System.Timers.Timer"),
                Allowed("Clicalo.Platform.Windows", "Clicalo.Infrastructure"),
                "time goes through TimeProvider and ids through IIdGenerator; only adapters touch the OS sources (§4.4, §13, NFR-013)"
            ),
        ];
    }

    private static Func<IMember, bool> Nothing => _ => false;

    private static Func<IMember, bool> FileWrites =>
        Either(
            Calls(
                "System.IO.File",
                "WriteAllText",
                "WriteAllTextAsync",
                "WriteAllBytes",
                "WriteAllBytesAsync",
                "WriteAllLines",
                "WriteAllLinesAsync",
                "AppendAllText",
                "AppendAllTextAsync",
                "AppendAllBytes",
                "AppendAllBytesAsync",
                "AppendAllLines",
                "AppendAllLinesAsync",
                "AppendText",
                "Create",
                "CreateText",
                "Open",
                "OpenWrite",
                "OpenHandle"
            ),
            Calls("System.IO.FileInfo", "Create", "CreateText", "AppendText", "Open", "OpenWrite"),
            Calls("System.IO.FileStream", ".ctor"),
            member =>
                IsOn(member, "System.IO.StreamWriter")
                && member.Name.StartsWith(".ctor(System.String", StringComparison.Ordinal)
        );

    private static Func<IMember, bool> ClockAndIdentity =>
        Either(
            Calls("System.DateTime", "get_Now", "get_UtcNow", "get_Today"),
            Calls("System.DateTimeOffset", "get_Now", "get_UtcNow"),
            Calls("System.Environment", "get_TickCount", "get_TickCount64"),
            Calls("System.Diagnostics.Stopwatch", "StartNew", ".ctor", "GetTimestamp"),
            Exactly("System.Diagnostics.Stopwatch", "GetElapsedTime(System.Int64)"),
            WithoutTimeProvider("System.Threading.Tasks.Task", "Delay", "WaitAsync"),
            WithoutTimeProvider("System.Threading.Tasks.Task`1", "WaitAsync"),
            Calls("System.Threading.Thread", "Sleep"),
            Calls("System.Guid", "NewGuid", "CreateVersion7"),
            Calls("System.Random", "get_Shared"),
            Exactly("System.Random", ".ctor()"),
            Exactly(
                "System.Threading.CancellationTokenSource",
                ".ctor(System.TimeSpan)",
                ".ctor(System.Int32)"
            ),
            Exactly("System.Threading.PeriodicTimer", ".ctor(System.TimeSpan)")
        );

    /// <summary>Calls to any overload of the named members of <paramref name="declaringType"/>.</summary>
    private static Func<IMember, bool> Calls(string declaringType, params string[] names) =>
        member =>
            IsOn(member, declaringType)
            && names.Any(name => member.Name.StartsWith(name + "(", StringComparison.Ordinal));

    /// <summary>Calls to exactly the given overloads (ArchUnitNET member names include the parameter types).</summary>
    private static Func<IMember, bool> Exactly(string declaringType, params string[] signatures) =>
        member =>
            IsOn(member, declaringType) && signatures.Contains(member.Name, StringComparer.Ordinal);

    /// <summary>Overloads of the named members that do not take a TimeProvider (a TimeSpan-only wait uses the system clock).</summary>
    private static Func<IMember, bool> WithoutTimeProvider(
        string declaringType,
        params string[] names
    ) =>
        member =>
            Calls(declaringType, names)(member)
            && !member.Name.Contains("System.TimeProvider", StringComparison.Ordinal)
            && (
                member.Name.Contains("System.TimeSpan", StringComparison.Ordinal)
                || member.Name.Contains("System.Int32", StringComparison.Ordinal)
            );

    private static Func<IMember, bool> Either(params Func<IMember, bool>[] matchers) =>
        member => matchers.Any(matcher => matcher(member));

    private static bool IsOn(IMember member, string declaringType) =>
        string.Equals(member.DeclaringType.FullName, declaringType, StringComparison.Ordinal);

    private static ImmutableArray<string> Uses(params string[] typesOrNamespaces) =>
        [.. typesOrNamespaces];
}
