using System.IO;
using Clicalo.App.Lifecycle;
using Clicalo.Application.Confirmation;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// «Desinstalar Clícalo» in the running instance (NFR-010, proposal P6, ADR-0029), over a fake uninstaller and a
/// temporary data folder: the data is only marked for deletion when asked, the instance ends cleanly before the
/// uninstaller starts, and a start Windows refuses takes the request back.
/// </summary>
[Trait("Req", "NFR-010")]
public sealed class SystemUninstallTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-uninstall-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly DataLocations _locations;
    private readonly List<string> _events = [];
    private bool _available = true;
    private bool _starts = true;

    public SystemUninstallTests()
    {
        _locations = new DataLocations(Path.Combine(_root, "roaming", "Clicalo"))
        {
            LocalRoot = Path.Combine(_root, "local", "Clicalo"),
        };
        Directory.CreateDirectory(_locations.Root);
        File.WriteAllText(_locations.Document, "{}");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // The temporary folder is cleaned by the system.
        }
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task Keeping_the_data_ends_the_instance_and_then_starts_the_uninstaller()
    {
        var uninstall = Uninstall();

        var started = await uninstall.UninstallAsync(false, Token(), Cancel);

        started.ShouldBeTrue();
        _events.ShouldBe(["exit", "uninstaller"], "every key is released and saved first");
        UninstallDataWipe.IsRequested(_locations).ShouldBeFalse();
        File.Exists(_locations.Document).ShouldBeTrue("the running instance never deletes data");
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public async Task Deleting_the_data_only_leaves_the_request_for_the_uninstaller()
    {
        var started = await Uninstall().UninstallAsync(true, Token(), Cancel);

        started.ShouldBeTrue();
        UninstallDataWipe.IsRequested(_locations).ShouldBeTrue();
        File.Exists(_locations.Document).ShouldBeTrue();
        UninstallDataWipe.RunIfRequested(_locations).ShouldBeTrue();
        File.Exists(_locations.Document).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task A_refused_start_takes_the_request_back()
    {
        _starts = false;

        _ = await Uninstall().UninstallAsync(true, Token(), Cancel);

        UninstallDataWipe
            .IsRequested(_locations)
            .ShouldBeFalse("a later uninstall from Windows Settings must keep the data");
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public async Task Another_copy_or_another_confirmation_uninstalls_nothing()
    {
        var other = Token("RestoreBackup");
        (await Uninstall().UninstallAsync(true, other, Cancel)).ShouldBeFalse();
        _available = false;
        Uninstall().IsAvailable.ShouldBeFalse();
        (await Uninstall().UninstallAsync(true, Token(), Cancel)).ShouldBeFalse();

        _events.ShouldBeEmpty();
        UninstallDataWipe.IsRequested(_locations).ShouldBeFalse();
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static ConfirmationToken Token(string operation = ISystemUninstall.Operation)
    {
        var confirm = new TwoStepConfirm(TimeProvider.System);
        var subject = new ConfirmationSubject(operation, "keep-data");
        _ = confirm.Tap(subject);
        return ((TwoStepResult.Confirmed)confirm.Tap(subject)).Token;
    }

    private SystemUninstall Uninstall() =>
        new(
            () => _available,
            () =>
            {
                _events.Add("uninstaller");
                return _starts;
            },
            _locations,
            new AtomicFile(TimeProvider.System, NullLogger<AtomicFile>.Instance),
            last =>
            {
                _events.Add("exit");
                last();
                return Task.CompletedTask;
            }
        );
}
