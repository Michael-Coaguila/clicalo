using Microsoft.Extensions.DependencyInjection;

namespace Clicalo.App.Composition;

/// <summary>
/// Chooses the Win32 side of the engine (<see cref="EngineAdapterSet"/>). With <c>--no-input</c> nothing can reach
/// the keyboard: a dry-run injector, a detached ledger, internal chords that never inject, no guardian and no
/// preventive release. Otherwise the engine package's adapters are used.
/// </summary>
/// <remarks>
/// <para>
/// The sending set is composed from the engine package's pieces, which merge into <c>m2/skeleton</c> before this
/// package (docs/testing/spikes/M2-ownership.md): the ledger section (<c>KeyLedgerSection.CreateForEngine</c>), the
/// fence (<c>new InjectionGate(section, new LowLevelInjector())</c>), and from <c>Platform.Windows/Input</c> and
/// <c>Platform.Windows/SentinelHost</c> the <c>IInputInjector</c> and <c>IKeyLedger</c> adapters over them, the
/// <c>IInternalKeyEffects</c> behind the fence, the preventive release with the menu mask, Sentinel's launcher over
/// <c>section.DuplicateForGuardian()</c> and the layout builder. Until that merge, <see cref="Create"/> refuses to
/// start a sending instance: Clícalo never presses a key without the ledger and the guardian behind it (ADR-0004).
/// </para>
/// </remarks>
internal static class EngineAdapters
{
    /// <summary>The set for the command line of <paramref name="services"/>.</summary>
    /// <param name="services">
    /// The container: the command line, and the pieces the sending set is built over (the SysEvents thread and the
    /// internal rights hotkey of the foreground ladder, the clock, the loggers).
    /// </param>
    public static EngineAdapterSet Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.GetRequiredService<AppOptions>();
        if (!options.SendInput)
        {
            var nothing = new NoGuardian();
            return new EngineAdapterSet(
                new DryRunInputInjector(),
                new DetachedKeyLedger(),
                new DryRunKeyEffects(),
                nothing,
                nothing,
                NoResources.Instance
            );
        }

        throw new InvalidOperationException(
            "The engine's Win32 adapters (Platform.Windows/Input and SentinelHost) are not part of this build yet: "
                + "start with "
                + AppOptions.NoInputOption
                + ", or build after the engine package is integrated (docs/testing/spikes/M2-ownership.md)."
        );
    }

    /// <summary>The layout builder of the foreground describer.</summary>
    public static Func<uint, Clicalo.Domain.Execution.KeyboardLayoutSnapshot> Layouts() =>
        ForegroundDescriber.LayoutOnly;

    private sealed class NoResources : IDisposable
    {
        public static NoResources Instance { get; } = new();

        public void Dispose() { }
    }
}
