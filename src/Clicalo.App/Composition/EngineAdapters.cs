using System.IO;
using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Input;
using Clicalo.Platform.Windows.SentinelHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Composition;

/// <summary>
/// Chooses the Win32 side of the engine (<see cref="EngineAdapterSet"/>). With <c>--no-input</c> nothing can reach
/// the keyboard: a dry-run injector, internal chords that never inject, no guardian and no release. Otherwise the
/// engine package's adapters are used.
/// </summary>
/// <remarks>
/// The sending set (ADR-0023): the only <c>SendInput</c> (<see cref="LowLevelInjector"/>) behind the
/// <see cref="InputInjector"/> of <c>Platform.Windows/Input</c>, the <c>IInternalKeyEffects</c> the engine sends
/// (<see cref="EngineKeyEffects"/>), the release of what Windows reports down (<see cref="SystemPressedRelease"/>)
/// and Sentinel's supervisor, which starts <c>Clicalo.Sentinel.exe</c> from the folder of <c>Clicalo.exe</c>.
/// </remarks>
internal static class EngineAdapters
{
    /// <summary>The guardian's executable, next to <c>Clicalo.exe</c>.</summary>
    public const string SentinelFileName = "Clicalo.Sentinel.exe";

    /// <summary>The set for the command line of <paramref name="services"/>.</summary>
    /// <param name="services">
    /// The container: the command line, and the pieces the sending set is built over (the internal rights hotkey of
    /// the foreground ladder, the clock, the loggers).
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
                new DryRunKeyEffects(),
                nothing,
                nothing,
                NoResources.Instance
            );
        }

        var sender = new LowLevelInjector();
        var supervisor = new SentinelSupervisor(
            Path.Combine(AppContext.BaseDirectory, SentinelFileName),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<SentinelSupervisor>>()
        );
        return new EngineAdapterSet(
            new InputInjector(sender, SystemKeyState.Instance),
            new EngineKeyEffects(
                services.GetRequiredService<IEngineInbox>(),
                services.GetRequiredService<InternalChordReplies>(),
                services.GetRequiredService<InternalRightsHotkey>(),
                services.GetRequiredService<TimeProvider>()
            ),
            new SupervisedGuardian(supervisor),
            new SystemPressedRelease(sender),
            supervisor
        );
    }

    /// <summary>
    /// The layout builder of the foreground describer: the layout of the thread with its character table
    /// (<c>VkKeyScanEx</c>, <c>MapVirtualKeyEx</c>; §7.7). It only queries Windows, so a start without key sending
    /// uses it too.
    /// </summary>
    public static Func<uint, Clicalo.Domain.Execution.KeyboardLayoutSnapshot> Layouts() =>
        KeyboardLayoutCapture.ForThread;

    private sealed class NoResources : IDisposable
    {
        public static NoResources Instance { get; } = new();

        public void Dispose() { }
    }
}
