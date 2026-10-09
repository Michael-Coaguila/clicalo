using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary>
/// The update service over a fake channel (ACT-001 to ACT-005, NFR-010, ADR-0027): states, never down by itself, the
/// backup and the clean exit before installing, the idle wait, and the rollback window with its two taps.
/// </summary>
public sealed class UpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeUpdateClient _client = new();
    private readonly MemoryUpdateState _state = new();
    private UpdateSettings _settings = new(true, true, true, UpdateChannel.Stable);
    private TimeSpan _idle = TimeSpan.Zero;
    private bool _backupWorks = true;
    private int _backups;
    private int _exits;

    [Fact]
    [Trait("Req", "ACT-001")]
    public async Task A_copy_that_was_not_installed_has_no_updates()
    {
        _client.IsInstalled = false;
        using var service = Create();

        await service.StartAsync(CancellationToken.None);
        await service.CheckAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.Unavailable);
        _client.Finds.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ACT-001")]
    [Trait("Req", "ACT-004")]
    public async Task The_start_checks_and_offers_a_newer_version_with_its_notes()
    {
        _client.Feed[UpdateChannel.Stable].AddRange(["1.9.0", "2.1.0"]);
        using var service = Create();

        await service.StartAsync(CancellationToken.None);

        var status = service.Status;
        status.Phase.ShouldBe(UpdatePhase.Found);
        status.HasNewVersion.ShouldBeTrue("the counter of Sistema");
        status.NewVersion.ShouldBe("2.1.0");
        status.LastChecked.ShouldBe(Now);
        status.Notes.Select(n => (n.Version, n.IsNew)).ShouldBe([("2.1.0", true), ("2.0.0", false)]);
        status.Notes[0].In("en").ShouldBe(["News of 2.1.0"]);
        status.Notes[0].Date.ShouldBe(new DateOnly(2026, 10, 1));
        _state.State.ShouldBe(new UpdateState("2.0.0", null, null));
        _client.Applied.ShouldBeEmpty("«Avisar antes» is on: nothing installs by itself");
    }

    [Fact]
    [Trait("Req", "ACT-002")]
    public async Task Without_automatic_updates_the_start_does_not_check()
    {
        _settings = _settings with { Automatic = false };
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        using var service = Create();

        await service.StartAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
        service.Status.LastChecked.ShouldBeNull();
        _client.Finds.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ACT-002")]
    [Trait("Req", "NFR-010")]
    public async Task The_channel_never_goes_down_by_itself()
    {
        _client.CurrentVersion = "2.1.0-beta.2";
        _client.Feed[UpdateChannel.Stable].Add("2.0.0");
        _client.Feed[UpdateChannel.Beta].Add("2.1.0-beta.1");
        using var service = Create();

        await service.CheckAsync(CancellationToken.None);
        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
        _settings = _settings with { Channel = UpdateChannel.Beta };
        await service.CheckAsync(CancellationToken.None);
        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        _settings = _settings with { Channel = UpdateChannel.Stable };
        await service.CheckAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.Found, "Estable caught up with the beta");
        service.Status.NewVersion.ShouldBe("2.1.0");
    }

    [Fact]
    [Trait("Req", "ACT-003")]
    public async Task Installing_backs_up_then_hands_over_to_the_updater_and_exits()
    {
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        using var service = Create();
        var percents = new List<int>();
        service.StatusChanged += (_, _) => percents.Add(service.Status.Percent);
        await service.CheckAsync(CancellationToken.None);

        await service.InstallAsync(CancellationToken.None);

        _client.Downloaded.ShouldBe(["2.1.0"]);
        _backups.ShouldBe(1);
        _client.Applied.ShouldBe(["2.1.0"]);
        _exits.ShouldBe(1);
        service.Status.Phase.ShouldBe(UpdatePhase.Installing);
        percents.ShouldContain(50);
        percents.ShouldContain(100);
    }

    [Fact]
    [Trait("Req", "ACT-003")]
    public async Task Without_the_backup_nothing_is_installed()
    {
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        _backupWorks = false;
        using var service = Create();
        await service.CheckAsync(CancellationToken.None);

        await service.InstallAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.Failed);
        service.Status.Error.ShouldBe(UpdateError.Interrupted);
        _client.Applied.ShouldBeEmpty();
        _exits.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "ACT-002")]
    public async Task The_backup_before_updating_can_be_turned_off()
    {
        _settings = _settings with { BackupBefore = false };
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        using var service = Create();
        await service.CheckAsync(CancellationToken.None);

        await service.InstallAsync(CancellationToken.None);

        _backups.ShouldBe(0);
        _client.Applied.ShouldBe(["2.1.0"]);
    }

    [Theory]
    [InlineData(UpdateError.Damaged)]
    [InlineData(UpdateError.NoSpace)]
    [InlineData(UpdateError.Offline)]
    [Trait("Req", "ACT-001")]
    public async Task A_failed_download_shows_its_reason_and_installs_nothing(UpdateError error)
    {
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        _client.DownloadFails = error;
        using var service = Create();
        await service.CheckAsync(CancellationToken.None);

        await service.InstallAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.Failed);
        service.Status.Error.ShouldBe(error);
        _client.Applied.ShouldBeEmpty();
        _exits.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "ACT-001")]
    public async Task No_connection_is_an_error_that_can_be_retried()
    {
        _client.FindFails = UpdateError.Offline;
        using var service = Create();

        await service.CheckAsync(CancellationToken.None);
        service.Status.Phase.ShouldBe(UpdatePhase.Failed);
        service.Status.Error.ShouldBe(UpdateError.Offline);
        _client.FindFails = null;
        await service.CheckAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
        service.Status.Error.ShouldBe(UpdateError.None);
    }

    [Fact]
    [Trait("Req", "ACT-003")]
    [Trait("Req", "NFR-020")]
    public async Task Automatic_installation_waits_for_the_panel_to_rest()
    {
        _settings = _settings with { AskBefore = false };
        _idle = TimeSpan.FromMinutes(1);
        _client.Feed[UpdateChannel.Stable].Add("2.1.0");
        using var service = Create();

        await service.StartAsync(CancellationToken.None);
        _client.Applied.ShouldBeEmpty("the panel was used a minute ago");
        _idle = Timings.Updates.UpdateIdleRequired;
        _time.Advance(Timings.Updates.UpdateIdleRequired - TimeSpan.FromMinutes(1));

        _client.Applied.ShouldBe(["2.1.0"]);
        _backups.ShouldBe(1);
        _exits.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "ACT-001")]
    [Trait("Req", "ACT-005")]
    public async Task The_first_run_of_a_new_version_says_updated_and_offers_to_go_back()
    {
        _client.CurrentVersion = "2.1.0";
        _state.State = new UpdateState("2.0.0", null, null);
        _settings = _settings with { Automatic = false };
        using var service = Create();

        await service.StartAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.Updated);
        service.Status.RollbackVersion.ShouldBe("2.0.0");
        _state.State.ShouldBe(new UpdateState("2.1.0", "2.0.0", Now));
        service.Acknowledge();
        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public async Task Going_back_is_hidden_after_seven_days()
    {
        _client.CurrentVersion = "2.1.0";
        _state.State = new UpdateState(
            "2.1.0",
            "2.0.0",
            Now - Timings.Backups.PreviousVersionRetention
        );
        _settings = _settings with { Automatic = false };
        using var service = Create();

        await service.StartAsync(CancellationToken.None);

        service.Status.RollbackVersion.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    [Trait("Req", "REG-04")]
    public async Task Going_back_needs_the_second_tap_and_installs_exactly_the_previous_version()
    {
        _client.CurrentVersion = "2.1.0";
        _client.Feed[UpdateChannel.Stable].AddRange(["2.0.0", "2.1.0"]);
        _state.State = new UpdateState("2.1.0", "2.0.0", Now - TimeSpan.FromDays(1));
        _settings = _settings with { Automatic = false };
        using var service = Create();
        await service.StartAsync(CancellationToken.None);
        var confirm = new TwoStepConfirm(_time);

        var other = confirm.Tap(new ConfirmationSubject("DeleteShortcut", "2.0.0"));
        confirm.Tap(new ConfirmationSubject("DeleteShortcut", "2.0.0")).ShouldBeOfType<TwoStepResult.Confirmed>();
        other.ShouldBeOfType<TwoStepResult.Armed>();
        var subject = new ConfirmationSubject(UpdateStatus.RollbackOperation, "2.0.0");
        confirm.Tap(subject).ShouldBeOfType<TwoStepResult.Armed>();
        var token = confirm.Tap(subject).ShouldBeOfType<TwoStepResult.Confirmed>().Token;

        await service.RollbackAsync(token, CancellationToken.None);

        _client.Finds.ShouldBe([(UpdateChannel.Stable, (string?)"2.0.0")]);
        _client.Applied.ShouldBe(["2.0.0"]);
        _exits.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public async Task After_going_back_there_is_nothing_older_to_go_back_to()
    {
        _client.CurrentVersion = "2.0.0";
        _state.State = new UpdateState("2.1.0", "2.0.0", Now - TimeSpan.FromDays(1));
        _settings = _settings with { Automatic = false };
        using var service = Create();

        await service.StartAsync(CancellationToken.None);

        service.Status.Phase.ShouldBe(UpdatePhase.UpToDate);
        service.Status.RollbackVersion.ShouldBeNull();
        _state.State.ShouldBe(new UpdateState("2.0.0", null, null));
    }

    private UpdateService Create() =>
        new(
            _client,
            _state,
            _time,
            new UpdateHooks(
                () => _settings,
                () => _idle,
                _ =>
                {
                    _backups++;
                    return Task.FromResult(_backupWorks);
                },
                () =>
                {
                    _exits++;
                    return Task.CompletedTask;
                }
            ),
            NullLogger<UpdateService>.Instance
        );
}
