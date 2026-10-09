using System.Windows.Threading;
using Clicalo.App.Composition;
using Clicalo.Application.Interaction;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Updates;
using Clicalo.Platform.Windows.Elevation;
using Clicalo.Platform.Windows.Startup;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The life of «Sistema» (docs/05 §5, ADR-0027): the updates start once the panel is up (a check at start and every
/// 24 h with «Actualizar automáticamente»), «Iniciar con Windows» and «Reabrir como administrador» only ever use the
/// installed executable, and every way out (an update, the handover to the elevated instance) goes through
/// <see cref="IAppLifetime.ExitAsync"/>: release all, flush, exit code 0, so Sentinel does not relaunch.
/// </summary>
internal sealed class SystemLifecycle : IDisposable
{
    private readonly UpdateService _updates;

    private SystemLifecycle(UpdateService updates, SystemServices services)
    {
        _updates = updates;
        Services = services;
    }

    /// <summary>What «Sistema» works with.</summary>
    public SystemServices Services { get; }

    /// <summary>Builds the services of «Sistema» and starts the updates in the background.</summary>
    /// <param name="services">The container of the app.</param>
    /// <param name="lifetime">The only way out.</param>
    /// <param name="options">The command line: isolated data turns every system feature off.</param>
    /// <param name="ui">The UI dispatcher, where the exit runs.</param>
    /// <param name="time">The clock.</param>
    public static SystemLifecycle Start(
        IServiceProvider services,
        IAppLifetime lifetime,
        AppOptions options,
        Dispatcher ui,
        TimeProvider time
    )
    {
        var store = services.GetRequiredService<DocumentStore>();
        var locations = services.GetRequiredService<DataLocations>();
        var backups = services.GetRequiredService<IBackupService>();
        var scheduler = services.GetRequiredService<PersistenceScheduler>();
        var writer = services.GetRequiredService<IAtomicFileWriter>();
        var interaction = services.GetRequiredService<InteractionStore>();

        // ACT-003: «sin uso» is the time since the last interaction with the panel.
        var lastUse = time.GetTimestamp();
        interaction.Changed += (_, _) => Interlocked.Exchange(ref lastUse, time.GetTimestamp());

        // A run with isolated data (cl run) is never the installed copy: no updates, no Run entry, no elevation.
        var installed = options.IsolatedData ? null : Installation.InstalledExecutable();
        Task ExitAsync() => ui.InvokeAsync(lifetime.ExitAsync).Task.Unwrap();
        var updates = UpdateService.Create(
            locations,
            writer,
            time,
            new UpdateHooks(
                () => store.Current.Settings.Updates,
                () => time.GetElapsedTime(Interlocked.Read(ref lastUse)),
                async cancellationToken =>
                {
                    // ACT-003: the pre-update backup of the document, written by the Persistence consumer.
                    var started = time.GetUtcNow();
                    backups.SnapshotNow(store.Current, BackupKind.PreUpdate);
                    await scheduler.FlushAsync(cancellationToken).ConfigureAwait(false);
                    var list = await backups.ListAsync(cancellationToken).ConfigureAwait(false);
                    return list.Any(b =>
                        b.Kind == BackupKind.PreUpdate
                        && b.CreatedAt >= started.AddTicks(-TimeSpan.TicksPerSecond)
                    );
                },
                ExitAsync
            ),
            services.GetRequiredService<ILogger<UpdateService>>(),
            enabled: !options.IsolatedData
        );
        _ = Task.Run(() => updates.StartAsync(CancellationToken.None));
        var system = new SystemServices(
            updates,
            new SystemBackups(locations, backups, scheduler, writer, time),
            new StartupRegistration(installed),
            new ElevatedRelaunch(installed),
            services.GetRequiredService<IIdGenerator>(),
            ExitAsync
        );
        return new SystemLifecycle(updates, system);
    }

    /// <inheritdoc />
    public void Dispose() => _updates.Dispose();
}
