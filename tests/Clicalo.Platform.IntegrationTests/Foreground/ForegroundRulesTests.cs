using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.SysEvents;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The rules of the foreground monitor that need no desktop: which windows of <c>explorer.exe</c> are File Explorer
/// (CAT-007) and how the app behind a Store app frame is looked for again (PER-002). Headless: windows and processes
/// are numbers.
/// </summary>
public sealed class ForegroundRulesTests
{
    [Theory]
    [InlineData("CabinetWClass", "explorer.exe", false)]
    [InlineData("CabinetWClass", "Explorer.EXE", false)]
    [InlineData("ExploreWClass", "explorer.exe", false)]
    [InlineData("Progman", "explorer.exe", true)]
    [InlineData("WorkerW", "explorer.exe", true)]
    [InlineData("Shell_TrayWnd", "explorer.exe", true)]
    [InlineData("#32770", "explorer.exe", true)]
    [InlineData("OperationStatusWindow", "explorer.exe", true)]
    [InlineData("", "explorer.exe", true)]
    [Trait("Req", "CAT-007")]
    public void Of_explorer_only_the_folder_windows_are_an_app(
        string className,
        string image,
        bool skipped
    )
    {
        NonAppWindows.Contains(className, image).ShouldBe(skipped);
    }

    [Theory]
    [InlineData("Notepad", "notepad.exe")]
    [InlineData("#32770", "notepad.exe")]
    [InlineData("CabinetWClass", "totalcmd.exe")]
    [InlineData("ApplicationFrameWindow", "ApplicationFrameHost.exe")]
    [InlineData("Chrome_WidgetWin_1", null)]
    [Trait("Req", "CAT-007")]
    public void The_windows_of_other_programs_are_apps_whatever_their_class(
        string className,
        string? image
    )
    {
        NonAppWindows.Contains(className, image).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-002")]
    public void The_app_behind_a_frame_is_reported_the_first_time_it_is_there()
    {
        var world = new FrameWorld { Foreground = 0x500 };

        world.Watch.Watch(0x500);
        world.Time.Advance(Timings.Foreground.HostedAppRecheck[0]);
        world.Resolved.ShouldBeEmpty();
        world.Checks.ShouldBe(1);

        world.Hosted = 4242;
        world.Time.Advance(Timings.Foreground.HostedAppRecheck[1]);

        world.Resolved.ShouldBe([((nint)0x500, 4242u)]);
        world.Watch.Frame.ShouldBe((nint)0);
        world.Time.Advance(TimeSpan.FromMinutes(1));
        world.Checks.ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "PER-002")]
    public void It_stops_asking_when_another_window_comes_to_the_front()
    {
        var world = new FrameWorld { Foreground = 0x500 };
        world.Watch.Watch(0x500);

        world.Foreground = 0x600;
        world.Hosted = 4242;
        world.Time.Advance(TimeSpan.FromMinutes(1));

        world.Resolved.ShouldBeEmpty();
        world.Checks.ShouldBe(0);
        world.Watch.Frame.ShouldBe((nint)0);
    }

    [Fact]
    [Trait("Req", "PER-002")]
    public void It_gives_up_after_its_last_wait_until_the_next_foreground_change()
    {
        var world = new FrameWorld { Foreground = 0x500 };
        world.Watch.Watch(0x500);

        foreach (var wait in Timings.Foreground.HostedAppRecheck)
        {
            world.Time.Advance(wait);
        }

        world.Time.Advance(TimeSpan.FromMinutes(1));

        world.Checks.ShouldBe(Timings.Foreground.HostedAppRecheck.Length);
        world.Resolved.ShouldBeEmpty();
        world.Watch.Frame.ShouldBe((nint)0);
    }

    [Fact]
    [Trait("Req", "PER-002")]
    public void Cancelling_or_disposing_asks_no_more()
    {
        var world = new FrameWorld { Foreground = 0x500, Hosted = 4242 };
        world.Watch.Watch(0x500);
        world.Watch.Cancel();
        world.Time.Advance(TimeSpan.FromMinutes(1));

        world.Watch.Watch(0x500);
        world.Watch.Dispose();
        world.Time.Advance(TimeSpan.FromMinutes(1));

        world.Checks.ShouldBe(0);
        world.Resolved.ShouldBeEmpty();
    }

    private sealed class FrameWorld
    {
        public FrameWorld() =>
            Watch = new HostedAppWatch(
                Time,
                static work => work(),
                _ =>
                {
                    Checks++;
                    return Hosted;
                },
                () => Foreground,
                (frame, process) => Resolved.Add((frame, process))
            );

        public FakeTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));

        public HostedAppWatch Watch { get; }

        public nint Foreground { get; set; }

        public uint Hosted { get; set; }

        public int Checks { get; private set; }

        public List<(nint Frame, uint Process)> Resolved { get; } = [];
    }
}
