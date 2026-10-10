using System.Collections.Immutable;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.SystemSection;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The Control Center world with «Sistema» composed over fakes: an update service whose status the test sets, backups
/// in memory with a file to import, a <c>Run</c> key in memory and a UAC prompt that answers what the test says.
/// Nothing touches the disk, the registry, the network or the desktop.
/// </summary>
internal sealed class SystemTestWorld
{
    public SystemTestWorld()
    {
        Base = new ControlCenterTestWorld();
        Services = Base.Services with
        {
            System = new SystemServices(Updates, Backups, Startup, Elevation, new Ids(), EndAsync),
        };
    }

    public ControlCenterTestWorld Base { get; }

    public ControlCenterServices Services { get; }

    public FakeUpdates Updates { get; } = new();

    public FakeBackups Backups { get; } = new();

    public FakeStartup Startup { get; } = new();

    public FakeElevation Elevation { get; } = new();

    public int Ended { get; private set; }

    private Task EndAsync()
    {
        Ended++;
        return Task.CompletedTask;
    }

    internal sealed class FakeUpdates : IUpdateService
    {
        private UpdateStatus _status = new(
            UpdatePhase.UpToDate,
            "2.0.0",
            null,
            0,
            UpdateError.None,
            null,
            null,
            []
        );

        public event EventHandler? StatusChanged;

        public UpdateStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public List<string> Calls { get; } = [];

        public ConfirmationToken? RollbackToken { get; private set; }

        public Task CheckAsync(CancellationToken cancellationToken)
        {
            Calls.Add("check");
            return Task.CompletedTask;
        }

        public Task InstallAsync(CancellationToken cancellationToken)
        {
            Calls.Add("install");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(ConfirmationToken token, CancellationToken cancellationToken)
        {
            Calls.Add("rollback");
            RollbackToken = token;
            return Task.CompletedTask;
        }

        public void Acknowledge() => Calls.Add("ack");
    }

    internal sealed class FakeBackups : ISystemBackups
    {
        public string Folder => "%APPDATA%\\Clicalo\\backups";

        public List<BackupInfo> List { get; } = [];

        public Dictionary<BackupId, UserDocument> Files { get; } = [];

        public Result<ImportPick>? Pick { get; set; }

        public int Created { get; private set; }

        public ExportOutcome Export { get; set; } = ExportOutcome.Done;

        public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(List.ToImmutableArray());

        public Task<bool> CreateAsync(UserDocument document, CancellationToken cancellationToken)
        {
            Created++;
            var id = new BackupId(
                "manual/" + Created.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            List.Insert(
                0,
                new BackupInfo(
                    id,
                    BackupKind.Manual,
                    new DateTimeOffset(2026, 10, 9, 11, 0, 0, TimeSpan.Zero),
                    document.Library.Profiles.Count,
                    document.Library.Profiles.Sum(p => p.Shortcuts.Count)
                )
            );
            Files[id] = document;
            return Task.FromResult(true);
        }

        public Task<Result<UserDocument>> ReadAsync(
            BackupId id,
            CancellationToken cancellationToken
        ) => Task.FromResult(Results.Ok(Files[id]));

        public Task<ExportOutcome> ExportAsync(
            UserDocument document,
            CancellationToken cancellationToken
        ) => Task.FromResult(Export);

        public Task<Result<ImportPick>?> PickImportAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Pick);
    }

    internal sealed class FakeStartup : IStartupRegistration
    {
        public bool IsAvailable { get; set; } = true;

        public bool IsEnabled { get; private set; }

        public bool TrySetEnabled(bool enabled)
        {
            if (!IsAvailable)
            {
                return false;
            }

            IsEnabled = enabled;
            return true;
        }
    }

    internal sealed class FakeElevation : IElevatedRelaunch
    {
        public bool IsElevated { get; set; }

        public ElevationOutcome Answer { get; set; } = ElevationOutcome.Started;

        public int Asked { get; private set; }

        public Task<ElevationOutcome> RelaunchAsync(CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(Answer);
        }
    }

    private sealed class Ids : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() =>
            new("ip" + (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture));

        public ShortcutId NewShortcutId() =>
            new("is" + (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
