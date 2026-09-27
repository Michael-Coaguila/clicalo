using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using Clicalo.Architecture.Tests.Support;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// The rules of blueprint §4.4, mechanism 2, configured for the product. Each rule is built from a shape in
/// <see cref="DependencyRules"/>, <see cref="ModuleMatrix"/> or <see cref="ConfinedApis"/>; the confinement rules
/// take a <see cref="Scope"/> so the negative tests run them unchanged on violating fixtures.
/// </summary>
internal static class ProductRules
{
    /// <summary>WPF and Windows Forms: their assemblies, and the System.Windows and System.Xaml namespaces.</summary>
    public static Zone UiFramework { get; } =
        Zone.Where(
            "WPF and Windows Forms",
            type =>
                IsUiFrameworkAssembly(Zone.AssemblyNameOf(type))
                || Zone.IsInNamespace(type, "System.Windows")
                || Zone.IsInNamespace(type, "System.Xaml")
        );

    /// <summary>
    /// <c>System.Windows.Input.ICommand</c> lives in the BCL (System.ObjectModel) and is the portable MVVM contract
    /// that CommunityToolkit.Mvvm implements, so Presentation may use it.
    /// </summary>
    public static Zone PortableCommand { get; } = Zone.Type("System.Windows.Input.ICommand");

    /// <summary>OS and UI access that the pure Domain never has (blueprint §4.2).</summary>
    public static Zone OsAccess { get; } =
        Zone.AnyOf(
            "System.IO, System.Net, Microsoft.Win32 and System.Windows",
            Zone.Namespace("System.IO"),
            Zone.Namespace("System.Net"),
            Zone.Namespace("Microsoft.Win32"),
            Zone.Namespace("System.Windows")
        );

    /// <summary>Types outside the base class library and outside Clicalo.Domain.</summary>
    public static Zone OutsideDomainAndBcl { get; } =
        Zone.Where(
            "assemblies outside the BCL",
            type => !Product.Domain.Contains(type) && !IsBaseClassLibrary(Zone.AssemblyNameOf(type))
        );

    public static IArchRule DomainUsesOnlyTheBaseClassLibrary { get; } =
        DependencyRules.NotDependOn(
            Product.Domain,
            OutsideDomainAndBcl,
            "the Domain is pure: BCL only, including System.Collections.Immutable and Frozen (§4.2)"
        );

    public static IArchRule DomainHasNoOsAccess { get; } =
        DependencyRules.NotDependOn(
            Product.Domain,
            OsAccess,
            "the Domain never touches files, the network, the registry or a UI (§4.2)"
        );

    public static IArchRule ApplicationHasNoUiFramework { get; } =
        DependencyRules.NotDependOn(
            Product.Application,
            UiFramework,
            "Application never depends on WPF (§4.2)"
        );

    public static IArchRule PresentationHasNoUiFramework { get; } =
        DependencyRules.NotDependOn(
            Product.Presentation,
            UiFramework.Except(PortableCommand),
            "ViewModels never touch the UI framework, so it stays replaceable (§4.2, ADR-0001)"
        );

    public static IArchRule UiWpfUsesOnlyUiPortsOfApplication { get; } =
        DependencyRules.NotDependOn(
            Product.UiWpf,
            Product.Application.Except(Zone.Namespace("Clicalo.Application.Ports")),
            "UI.Wpf reaches Application only through the UI port types (§4.2)"
        );

    public static IArchRule UiWpfUsesCsWin32OnlyInWindowingAndPointer { get; } =
        DependencyRules.NotDependOn(
            Product.UiWpf.Except(
                Zone.AnyOf(
                    "Windowing, Pointer and the generated interop",
                    Zone.Namespace("Clicalo.UI.Wpf.Windowing"),
                    Zone.Namespace("Clicalo.UI.Wpf.Pointer"),
                    Product.GeneratedInterop
                )
            ),
            Product.GeneratedInterop,
            "Win32 in UI.Wpf is confined to Windowing/ and Pointer/ (§4.2)"
        );

    public static IArchRule InfrastructureUsesOnlyTrustOfPlatformCore { get; } =
        DependencyRules.NotDependOn(
            Product.Infrastructure,
            Product.PlatformCore.Except(Zone.Namespace("Clicalo.Platform.Core.Trust")),
            "Infrastructure only uses Platform.Core to verify signatures (§4.2)"
        );

    public static IArchRule LauncherUsesOnlyTrustOfPlatformCore { get; } =
        DependencyRules.NotDependOn(
            Product.Launcher,
            Product.PlatformCore.Except(Zone.Namespace("Clicalo.Platform.Core.Trust")),
            "the Launcher only verifies and starts the protected copy (§4.2, ADR-0009)"
        );

    public static IArchRule NothingDependsOnTheCompositionRoot { get; } =
        DependencyRules.NotDependOn(
            Product.All.Except(Product.App),
            Product.App,
            "nothing references Clicalo.App (§4.2)"
        );

    public static IArchRule ModulesKeepTheirInternalsPrivate { get; } =
        DependencyRules.NotUseOtherModulesInternals(
            Product.Code,
            "the public API of a module is the public types at its root; details are internal or in .Internal (§4.3)"
        );

    /// <summary>
    /// Types of a product project only use the product assemblies that allowed-dependencies.json lets it reference.
    /// </summary>
    /// <remarks>
    /// The universe is <see cref="Product.Code"/>, not <see cref="Product.All"/>: CsWin32 generates internal types with
    /// the same full name (<c>Windows.Win32.PInvoke</c>, <c>Foundation.HWND</c>…) in every assembly that uses it, and
    /// ArchUnitNET merges types by full name, so the interop of UI.Wpf and Platform.Windows would look like a
    /// dependency between the two. That interop is internal to each assembly and cannot cross it; where CsWin32 may be
    /// used is checked by its own rules.
    /// </remarks>
    public static IArchRule Layer(string project)
    {
        var entry = ArchitectureDocuments.AllowedDependencies().Projects[project];
        var allowed = entry.ProjectReferences.Select(Product.OfProject).ToArray();
        return DependencyRules.OnlyDependOn(
            Product.OfProject(project),
            Product.Code,
            allowed,
            entry.Rule
        );
    }

    /// <summary>Adapters implement only interfaces of Application that live in Application.Ports.</summary>
    public static IArchRule AdaptersImplementOnlyPorts(Scope scope) =>
        DependencyRules.ImplementOnlyPorts(
            Zone.AnyOf(
                "the adapters",
                scope.Namespace("Clicalo.Platform.Windows"),
                scope.Namespace("Clicalo.Infrastructure"),
                scope.Namespace("Clicalo.UI.Wpf")
            ),
            scope.Namespace("Clicalo.Application"),
            scope.Name("Clicalo.Application.Ports"),
            "ports live in Application.Ports (§4.4)"
        );

    /// <summary>Only Application.Engine, Platform.Windows and App depend on IInputInjector.</summary>
    public static IArchRule OnlyEnginePlatformAndAppUseTheInputInjector(Scope scope) =>
        DependencyRules.OnlyAllowedDependOn(
            TypeNamed("IInputInjector", scope.Namespace("Clicalo.Application")),
            Zone.AnyOf(
                "Application.Engine, Platform.Windows, App and the ports",
                scope.Namespace("Clicalo.Application.Engine"),
                scope.Namespace("Clicalo.Platform.Windows"),
                scope.Namespace("Clicalo.App"),
                scope.Namespace("Clicalo.Application.Ports")
            ),
            scope.Universe,
            "only the engine injects input, through InjectionGate (§4.4, ADR-0004)"
        );

    /// <summary>
    /// Only Application.Foreground depends on IForegroundControl, besides its implementation in
    /// Platform.Windows/Foreground and the composition root that registers it.
    /// </summary>
    public static IArchRule OnlyForegroundUsesTheForegroundControl(Scope scope) =>
        DependencyRules.OnlyAllowedDependOn(
            TypeNamed("IForegroundControl", scope.Namespace("Clicalo.Application")),
            Zone.AnyOf(
                "Application.Foreground, its adapter, App and the ports",
                scope.Namespace("Clicalo.Application.Foreground"),
                scope.Namespace("Clicalo.Platform.Windows.Foreground"),
                scope.Namespace("Clicalo.App"),
                scope.Namespace("Clicalo.Application.Ports")
            ),
            scope.Universe,
            "ForegroundOrchestrator is the only owner of foreground changes (§3.6, ADR-0005)"
        );

    /// <summary>
    /// Only Execution and the Control Center editor reveal a SecretText, plus the engine that sends it and the
    /// persistence mapper that encrypts it (blueprint §6.7).
    /// </summary>
    public static IArchRule OnlyExecutionAndTheEditorRevealSecrets(Scope scope)
    {
        var secretText = TypeNamed("SecretText", scope.Namespace("Clicalo.Domain"));
        return DependencyRules.OnlyAllowedCall(
            member =>
                secretText.Contains(member.DeclaringType)
                && member.Name.StartsWith("WithRevealed", StringComparison.Ordinal),
            "SecretText.WithRevealed",
            Zone.AnyOf(
                "Execution, the engine, the Control Center editor, the persistence mappers and SecretText itself",
                scope.Namespace("Clicalo.Domain.Execution"),
                scope.Namespace("Clicalo.Application.Engine"),
                scope.Namespace("Clicalo.Presentation.ControlCenter.Editor"),
                scope.Namespace("Clicalo.Infrastructure.Persistence"),
                secretText
            ),
            scope.Universe,
            "secret text is revealed only to be sent, edited or encrypted (§4.4, §6.2, §6.7, LOG-003)"
        );
    }

    /// <summary>
    /// Only the Surfaces role writes SessionStore and InteractionStore: outside it, code may read them (getters and
    /// events) but never call a constructor or a mutating member.
    /// </summary>
    public static IArchRule OnlyTheSurfacesRoleWritesSessionAndInteraction(Scope scope)
    {
        var stores = Zone.AnyOf(
            "SessionStore and InteractionStore",
            TypeNamed("SessionStore", scope.Namespace("Clicalo.Application")),
            TypeNamed("InteractionStore", scope.Namespace("Clicalo.Application"))
        );
        var surfacesRole = Zone.AnyOf(
            "the Surfaces role and the composition root",
            scope.Namespace("Clicalo.Application.Session"),
            scope.Namespace("Clicalo.Application.Interaction"),
            scope.Namespace("Clicalo.Application.Coordinators"),
            scope.Namespace("Clicalo.Presentation.Panel"),
            scope.Namespace("Clicalo.Presentation.Dock"),
            scope.Namespace("Clicalo.Presentation.Bubble"),
            scope.Namespace("Clicalo.Presentation.SideWindows"),
            scope.Namespace("Clicalo.UI.Wpf.Surfaces"),
            scope.Namespace("Clicalo.UI.Wpf.Pointer"),
            scope.Namespace("Clicalo.UI.Wpf.Windowing"),
            scope.Namespace("Clicalo.App")
        );
        return DependencyRules.OnlyAllowedCall(
            member => stores.Contains(member.DeclaringType) && !IsReadAccessor(member),
            "a constructor or mutating member of SessionStore or InteractionStore",
            surfacesRole,
            scope.Universe,
            "SessionStore and InteractionStore have a single writer, the Surfaces role (§3.2, ADR-0003)"
        );
    }

    /// <summary>Application.Ipc depends neither on Engine nor on Foreground (invariant D13).</summary>
    public static IArchRule IpcDoesNotReachEngineOrForeground(Scope scope) =>
        DependencyRules.NotDependOn(
            scope.Namespace("Clicalo.Application.Ipc"),
            Zone.AnyOf(
                "Application.Engine and Application.Foreground",
                scope.Namespace("Clicalo.Application.Engine"),
                scope.Namespace("Clicalo.Application.Foreground")
            ),
            "the IPC server can neither inject nor take the foreground (D13, ADR-0010)"
        );

    /// <summary>Within Application, the IPC server only depends on IShellNavigator (invariant D13).</summary>
    public static IArchRule IpcUsesOnlyTheShellNavigator(Scope scope) =>
        DependencyRules.NotDependOn(
            scope.Namespace("Clicalo.Application.Ipc"),
            scope
                .Namespace("Clicalo.Application")
                .Except(
                    Zone.AnyOf(
                        "Application.Ipc and IShellNavigator",
                        scope.Namespace("Clicalo.Application.Ipc"),
                        scope.Type("Clicalo.Application.Ports.IShellNavigator")
                    )
                ),
            "the IPC server only shows the app and opens previews through IShellNavigator (D13, ADR-0010)"
        );

    /// <summary>
    /// The single-instance pipe that exists today (invariant D13): <c>Clicalo.App.SingleInstance</c>, the M2 server in
    /// the composition root (D-21), and <c>Platform.Windows.SingleInstance</c>, where it moves with the IPC of M4, reach
    /// neither the engine, the foreground, the document store nor the input. The composition root may depend on
    /// everything, so without this rule nothing would stop the server from posting to the engine.
    /// </summary>
    public static IArchRule SingleInstancePipeIsConfined(Scope scope) =>
        DependencyRules.NotDependOn(
            SingleInstancePipe(scope),
            Zone.AnyOf(
                "the engine, the foreground, the document store and the input",
                scope.Namespace("Clicalo.Application.Engine"),
                scope.Namespace("Clicalo.Application.Foreground"),
                scope.Namespace("Clicalo.Application.Store"),
                scope.Type("Clicalo.Application.Ports.IEngineInbox"),
                scope.Type("Clicalo.Application.Ports.IInputInjector"),
                scope.Type("Clicalo.Application.Ports.IClipboardPaster"),
                scope.Type("Clicalo.Application.Ports.IForegroundControl"),
                scope.Namespace("Clicalo.Platform.Core.Injection"),
                scope.Namespace("Clicalo.Platform.Windows.Foreground"),
                scope.Namespace("Clicalo.Platform.Windows.Input")
            ),
            "the single-instance pipe only shows the panel: it can neither inject, edit the document nor take the foreground (D13, ADR-0010, D-21)"
        );

    /// <summary>The namespaces of the single-instance pipe (see <see cref="SingleInstancePipeIsConfined"/>).</summary>
    public static Zone SingleInstancePipe(Scope scope) =>
        Zone.AnyOf(
            "the single-instance pipe",
            scope.Namespace("Clicalo.App.SingleInstance"),
            scope.Namespace("Clicalo.Platform.Windows.SingleInstance")
        );

    /// <summary>
    /// R4 (REG-04): the destructive document commands are exactly the closed list: every implementation of
    /// IDestructiveCommand is listed, and a listed command that exists implements it.
    /// </summary>
    public static IArchRule DestructiveCommandsAreTheClosedList(
        Scope scope,
        IReadOnlyCollection<string> commands
    ) =>
        Rule.For(
            scope.Namespace("Clicalo.Domain"),
            "implement IDestructiveCommand exactly when listed in destructive-operations.json",
            "destructive commands need two taps and a ConfirmationToken (REG-04, CLC0010)",
            type =>
            {
                var listed = commands.Contains(type.Name, StringComparer.Ordinal);
                var destructive = Implements(type, "IDestructiveCommand");
                return (listed, destructive) switch
                {
                    (false, true) =>
                    [
                        "implements IDestructiveCommand but is not in destructive-operations.json",
                    ],
                    (true, false) when !IsInterface(type) =>
                    [
                        "is in destructive-operations.json but does not implement IDestructiveCommand",
                    ],
                    _ => [],
                };
            }
        );

    /// <summary>
    /// R4 (REG-04): the destructive use cases are exactly the closed list: every use case marked [Destructive] is
    /// listed, and a listed use case that exists (named <c>X</c> or <c>XUseCase</c>) carries the attribute.
    /// </summary>
    public static IArchRule DestructiveUseCasesAreTheClosedList(
        Scope scope,
        IReadOnlyCollection<string> useCases
    ) =>
        Rule.For(
            scope.Namespace("Clicalo.Application"),
            "carry [Destructive] exactly when listed in destructive-operations.json",
            "destructive use cases need two taps and a ConfirmationToken (REG-04, CLC0010)",
            type =>
            {
                var name = type.Name.EndsWith("UseCase", StringComparison.Ordinal)
                    ? type.Name[..^"UseCase".Length]
                    : type.Name;
                var listed = useCases.Contains(name, StringComparer.Ordinal);
                var destructive = type.Attributes.Any(a =>
                    string.Equals(a.Name, "DestructiveAttribute", StringComparison.Ordinal)
                );
                return (listed, destructive) switch
                {
                    (false, true) =>
                    [
                        "is marked [Destructive] but is not in destructive-operations.json",
                    ],
                    (true, false) =>
                    [
                        "is in destructive-operations.json but is not marked [Destructive]",
                    ],
                    _ => [],
                };
            }
        );

    /// <summary>R7 (REG-07): an undo exemption names a document command, so the list cannot go stale silently.</summary>
    public static IArchRule UndoExemptionsNameDocumentCommands(
        Scope scope,
        IReadOnlyCollection<string> commands
    ) =>
        Rule.For(
            scope.Namespace("Clicalo.Domain"),
            "be a document command when named in undo-exemptions.json",
            "every exemption from UndoIntent.Record is a justified document command (REG-07)",
            type =>
                commands.Contains(type.Name, StringComparer.Ordinal)
                && !Implements(type, "IDocumentCommand")
                    ? ["is in undo-exemptions.json but does not implement IDocumentCommand"]
                    : []
        );

    private static bool Implements(IType type, string interfaceName) =>
        type.ImplementedInterfaces.Any(i =>
            string.Equals(i.Name, interfaceName, StringComparison.Ordinal)
        );

    private static bool IsInterface(IType type) => type is Interface;

    private static Zone TypeNamed(string name, Zone within) =>
        Zone.Where(
            name + " in " + within.Name,
            type =>
                string.Equals(type.Name, name, StringComparison.Ordinal) && within.Contains(type)
        );

    private static bool IsReadAccessor(IMember member) =>
        member.Name.StartsWith("get_", StringComparison.Ordinal)
        || member.Name.StartsWith("add_", StringComparison.Ordinal)
        || member.Name.StartsWith("remove_", StringComparison.Ordinal);

    private static bool IsUiFrameworkAssembly(string name) =>
        name
            is "PresentationCore"
                or "WindowsBase"
                or "System.Xaml"
                or "ReachFramework"
                or "System.Printing"
        || name.StartsWith("PresentationFramework", StringComparison.Ordinal)
        || name.StartsWith("UIAutomation", StringComparison.Ordinal)
        || name.StartsWith("System.Windows.", StringComparison.Ordinal);

    private static bool IsBaseClassLibrary(string name) =>
        name is "mscorlib" or "netstandard" or "System"
        || (name.StartsWith("System.", StringComparison.Ordinal) && !IsUiFrameworkAssembly(name));
}
