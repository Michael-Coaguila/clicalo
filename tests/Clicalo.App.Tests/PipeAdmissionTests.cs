using System.Security.Principal;
using Clicalo.App.SingleInstance;
using Clicalo.Platform.Windows.SingleInstance;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// The server's half of the two-way check of the single-instance pipe (blueprint §3.4, ADR-0010): only a client of this
/// session and this user is served, whatever the DACL lets through, and its integrity is read.
/// </summary>
[Trait("Req", "SIS-003")]
public sealed class PipeAdmissionTests
{
    private const uint Medium = 0x2000;
    private const uint High = 0x3000;

    private static readonly SecurityIdentifier User = new(
        "S-1-5-21-1000000000-2000000000-300000000-1001"
    );
    private static readonly SecurityIdentifier OtherUser = new(
        "S-1-5-21-1000000000-2000000000-300000000-1002"
    );
    private static readonly InstanceIdentity Identity = new(User, "0123456789abcdef", 3);

    [Fact]
    public void The_same_user_in_the_same_session_with_the_same_integrity_is_trusted() =>
        PipeAdmission
            .Of(new PipeClient(3, User, Medium), Identity, Medium)
            .ShouldBe(ClientTrust.Full);

    [Fact]
    public void A_client_of_less_integrity_may_only_show() =>
        PipeAdmission
            .Of(new PipeClient(3, User, Medium), Identity, High)
            .ShouldBe(ClientTrust.ShowOnly);

    [Fact]
    public void A_client_whose_integrity_cannot_be_read_may_only_show() =>
        PipeAdmission
            .Of(new PipeClient(3, User, null), Identity, Medium)
            .ShouldBe(ClientTrust.ShowOnly);

    [Fact]
    public void A_client_of_another_user_is_rejected_even_in_this_session() =>
        PipeAdmission
            .Of(new PipeClient(3, OtherUser, High), Identity, Medium)
            .ShouldBe(ClientTrust.Rejected);

    [Fact]
    public void A_client_whose_user_cannot_be_read_is_rejected() =>
        PipeAdmission
            .Of(new PipeClient(3, null, Medium), Identity, Medium)
            .ShouldBe(ClientTrust.Rejected);

    [Theory]
    [InlineData(4u)]
    [InlineData(null)]
    public void A_client_of_another_session_or_an_unknown_one_is_rejected(uint? session) =>
        PipeAdmission
            .Of(new PipeClient(session, User, Medium), Identity, Medium)
            .ShouldBe(ClientTrust.Rejected);

    [Fact]
    public void This_process_reads_its_own_user_and_a_medium_or_higher_integrity()
    {
        using var current = WindowsIdentity.GetCurrent();
        var self = (uint)Environment.ProcessId;

        ProcessIdentity.User(self).ShouldBe(current.User);
        ProcessIdentity.IntegrityLevel(self).ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(Medium);
        ProcessIdentity
            .ImagePath(self)
            .ShouldBe(Environment.ProcessPath, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task A_client_of_this_user_is_admitted_through_a_real_pipe_and_the_panel_is_shown()
    {
        using var current = WindowsIdentity.GetCurrent();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var identity = new InstanceIdentity(
            current.User!,
            // A name no running Clícalo uses: the test never talks to the user's instance.
            "t" + Guid.NewGuid().ToString("N")[..15],
            process.SessionId
        );
        await using var server = new ShowPipeServer(
            identity,
            TimeProvider.System,
            NullLogger<ShowPipeServer>.Instance
        );
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ShowRequested += (_, _) => shown.TrySetResult();
        server.Start();

        var outcome = await ShowPipeClient.RequestShowAsync(
            identity,
            Environment.ProcessPath!,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        outcome.ShouldBe(ShowOutcome.Shown);
        await shown.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        server.IsSquatted.ShouldBeFalse();
    }
}
