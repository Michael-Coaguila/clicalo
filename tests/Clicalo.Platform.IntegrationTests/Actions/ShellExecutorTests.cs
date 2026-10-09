using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Platform.Windows.Launch;
using Clicalo.Platform.Windows.SystemCommands;

namespace Clicalo.Platform.IntegrationTests.Actions;

/// <summary>
/// The Shell thread's launcher (EJE-011, EJE-016) without starting anything: what each request becomes for
/// <c>ShellExecute</c>, how the notices name it, the last check before starting (LOG-008) and the system commands that
/// are unknown. Headless and deterministic.
/// </summary>
[Trait("Req", "EJE-011")]
public sealed class ShellExecutorTests
{
    private static LaunchRequest.StartApp Exe(string path, string arguments = "") =>
        new(new AppTarget.Executable(path, arguments));

    [Fact]
    public void An_address_opens_as_it_is_with_the_default_browser()
    {
        var (file, arguments, directory) = ShellExecutor.Target(
            new LaunchRequest.OpenUrl(new Uri("https://example.com/inbox"))
        );

        file.ShouldBe("https://example.com/inbox");
        arguments.ShouldBeEmpty();
        directory.ShouldBeEmpty();
    }

    [Fact]
    public void An_executable_starts_in_its_own_folder_with_its_arguments_as_they_are()
    {
        var (file, arguments, directory) = ShellExecutor.Target(
            Exe("\"C:\\Program Files\\App\\app.exe\"", "--new-window \"a b\"")
        );

        file.ShouldBe("C:\\Program Files\\App\\app.exe");
        arguments.ShouldBe("--new-window \"a b\"");
        directory.ShouldBe("C:\\Program Files\\App");
    }

    [Fact]
    public void A_store_app_starts_from_the_apps_folder_by_its_id()
    {
        var request = new LaunchRequest.StartApp(
            new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
        );

        ShellExecutor
            .Target(request)
            .File.ShouldBe("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");
        ShellExecutor.DisplayName(request).ShouldBe("Microsoft.WindowsCalculator");
    }

    [Theory]
    [InlineData("C:\\Windows\\notepad.exe", "notepad")]
    [InlineData("calc", "calc")]
    public void An_app_is_named_by_its_file(string path, string expected) =>
        ShellExecutor.DisplayName(Exe(path)).ShouldBe(expected);

    [Fact]
    [Trait("Req", "LOG-008")]
    public void A_command_interpreter_never_starts_even_if_it_reached_the_shell_thread()
    {
        using var shell = new ShellExecutor(selfElevated: false);

        var result = shell.Start(new EffectId(7), Exe("C:\\Windows\\System32\\cmd.exe", "/c dir"));

        var failed = result.ShouldBeOfType<EngineEvent.LaunchFailed>();
        failed.Effect.ShouldBe(new EffectId(7));
        failed.Failure.Code.ShouldBe("launch.unsafe");
    }

    [Fact]
    [Trait("Req", "EJE-016")]
    public async Task An_unknown_system_command_answers_that_it_did_not_run()
    {
        using var shell = new ShellExecutor(selfElevated: false);
        var inbox = new ReplyInbox();

        shell.Run(new EffectId(3), new SystemCommandId("reboot"), inbox);

        var reply = await inbox.Reply.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken
        );
        reply.ShouldBe(new EngineEvent.SystemCommandCompleted(new EffectId(3), Succeeded: false));
    }

    [Fact]
    [Trait("Req", "EJE-016")]
    public void Locking_is_always_available_and_unknown_commands_never_are()
    {
        SystemCommandRunner.IsAvailable(SystemCommandRunner.Lock).ShouldBeTrue();
        SystemCommandRunner.IsAvailable(new SystemCommandId("reboot")).ShouldBeFalse();
    }

    [Theory]
    [Trait("Req", "EJE-016")]
    [InlineData(50, 10, 60)]
    [InlineData(95, 10, 100)]
    [InlineData(5, -10, 0)]
    public void A_brightness_step_stays_between_0_and_100(int current, int delta, int expected) =>
        WmiBrightness.Next(current, delta).ShouldBe((byte)expected);

    private sealed class ReplyInbox : IEngineInbox
    {
        public TaskCompletionSource<EngineEvent> Reply { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Post(EngineEvent engineEvent) => Reply.TrySetResult(engineEvent);
    }
}
