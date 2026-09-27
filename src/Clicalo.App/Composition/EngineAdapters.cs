using System.IO;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Input;
using Clicalo.Platform.Windows.SentinelHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Composition;

/// <summary>
/// Chooses the Win32 side of the engine (<see cref="EngineAdapterSet"/>). With <c>--no-input</c> nothing can reach
/// the keyboard: a dry-run injector, a detached ledger, internal chords that never inject, no guardian and no
/// preventive release. Otherwise the engine package's adapters are used.
/// </summary>
/// <remarks>
/// The sending set is the engine package's pieces: the ledger section (<see cref="KeyLedgerSection.CreateForEngine"/>),
/// the fence (<see cref="InjectionGate"/> over the only <c>SendInput</c>, <see cref="LowLevelInjector"/>), and from
/// <c>Platform.Windows/Input</c> and <c>Platform.Windows/SentinelHost</c> the <c>IInputInjector</c> and
/// <c>IKeyLedger</c> adapters over them, the <c>IInternalKeyEffects</c> behind the fence, the preventive release with
/// the menu mask and Sentinel's supervisor, which starts <c>Clicalo.Sentinel.exe</c> from the folder of
/// <c>Clicalo.exe</c> with the section duplicated read-only (ADR-0004, ADR-0018).
/// </remarks>
internal static class EngineAdapters
{
    /// <summary>The guardian's executable, next to <c>Clicalo.exe</c>.</summary>
    public const string SentinelFileName = "Clicalo.Sentinel.exe";

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

        var section = KeyLedgerSection.CreateForEngine();
        try
        {
            var gate = new InjectionGate(section, new LowLevelInjector());
            var supervisor = new SentinelSupervisor(
                section,
                Path.Combine(AppContext.BaseDirectory, SentinelFileName),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<ILogger<SentinelSupervisor>>()
            );
            return new EngineAdapterSet(
                new GateInputInjector(gate),
                new KeyLedgerPort(gate),
                new InternalKeyEffects(gate, services.GetRequiredService<InternalRightsHotkey>()),
                new SupervisedGuardian(supervisor),
                new GateStartupRelease(gate),
                new SendingResources(supervisor, section)
            )
            {
                Gate = gate,
            };
        }
        catch
        {
            section.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The layout builder of the foreground describer: the layout of the thread with its character table
    /// (<c>VkKeyScanEx</c>, <c>MapVirtualKeyEx</c>; §7.7). It only queries Windows, so a start without key sending
    /// uses it too.
    /// </summary>
    public static Func<uint, Clicalo.Domain.Execution.KeyboardLayoutSnapshot> Layouts() =>
        static thread =>
            KeyboardLayoutCapture.Capture(Interop.NativeMethods.GetKeyboardLayout(thread));

    private sealed class NoResources : IDisposable
    {
        public static NoResources Instance { get; } = new();

        public void Dispose() { }
    }
}
