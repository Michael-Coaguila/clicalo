using System.Collections.Immutable;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// «Pausar» and the ways back to the panel in the tray controller (BUR-003, BUR-004, BUR-005), headless: a real
/// SysEvents thread with its message-only window, but the icon is never shown, no menu opens and no global shortcut
/// is registered. What matters is what reaches the engine and that showing the panel never asks for the foreground
/// (REG-01).
/// </summary>
public sealed class TrayPauseTests : IDisposable
{
    private readonly SysEventsThread _thread = SysEventsThread.Start();
    private readonly RecordingInbox _engine = new();
    private readonly CountingForeground _foreground = new();
    private readonly TrayController _tray;
    private int _showHide;
    private int _pauseChanges;

    public TrayPauseTests()
    {
        _tray = new TrayController(
            new TrayIcon(_thread),
            new TrayMenuHost(_thread),
            _foreground,
            _engine,
            new KeyNames()
        );
        _tray.ShowHideRequested += (_, _) => _showHide++;
        _tray.PauseChanged += (_, _) => _pauseChanges++;
    }

    public void Dispose()
    {
        _tray.Dispose();
        _thread.Dispose();
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "SEG-007")]
    public void Pausing_is_a_terminal_event_so_the_engine_releases_everything_and_sends_nothing()
    {
        _tray.Run(TrayCommand.Pause);

        _tray.IsPaused.ShouldBeTrue();
        _engine
            .Posted.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Terminal>()
            .Reason.ShouldBe(TerminalReason.Pause);
        _pauseChanges.ShouldBe(1);
        _showHide.ShouldBe(0);
        Texts().ShouldBe(["restore", "cc", "releaseAll", "resumeApp", "exitApp"]);
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    public void Resume_tells_the_engine_to_send_again()
    {
        _tray.Run(TrayCommand.Pause);

        _tray.Run(TrayCommand.Pause);

        _tray.IsPaused.ShouldBeFalse();
        _engine.Posted[^1].ShouldBeOfType<EngineEvent.SetPaused>().On.ShouldBeFalse();
        _pauseChanges.ShouldBe(2);
        Texts().ShouldBe(["hidePanel", "cc", "releaseAll", "pauseApp", "exitApp"]);
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "BUR-005")]
    public void While_paused_the_icon_the_shortcut_and_show_panel_resume_instead_of_toggling()
    {
        _tray.Run(TrayCommand.Pause);

        _tray.TogglePanel();

        _tray.IsPaused.ShouldBeFalse();
        _showHide.ShouldBe(0);
        _engine.Posted[^1].ShouldBeOfType<EngineEvent.SetPaused>().On.ShouldBeFalse();

        _tray.Run(TrayCommand.Pause);
        _tray.Run(TrayCommand.ShowHide);
        _tray.IsPaused.ShouldBeFalse();
        _showHide.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "REG-01")]
    public void Showing_or_hiding_the_panel_never_asks_for_the_foreground_nor_touches_the_engine()
    {
        _tray.TogglePanel();
        _tray.Run(TrayCommand.ShowHide);

        _showHide.ShouldBe(2);
        _foreground.Requests.ShouldBe(0);
        _engine.Posted.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_state_of_the_panel_and_of_what_is_held_shows_in_the_menu()
    {
        _ = _tray.UpdateStateAsync(panelVisible: false, anythingHeld: true);

        _tray.State.ShouldBe(new TrayState(PanelVisible: false, AnythingHeld: true, Paused: false));
        Texts()[0].ShouldBe("restore");
        _tray.MenuItems().Single(i => i.Id == (int)TrayCommand.ReleaseAll).IsEnabled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BUR-005")]
    public async Task A_combination_a_global_shortcut_cannot_use_is_refused_without_registering_anything()
    {
        var withWindowsKey = Clicalo.Domain.Keys.KeyChord.Create([
            new Clicalo.Domain.Keys.KeyStroke(new Clicalo.Domain.Keys.KeyId("win")),
            new Clicalo.Domain.Keys.KeyStroke(new Clicalo.Domain.Keys.KeyId("space")),
        ]);

        (await _tray.SetHotkeyAsync(withWindowsKey)).ShouldBeFalse();
        (await _tray.SetHotkeyAsync(null)).ShouldBeTrue();
    }

    private string[] Texts() => [.. _tray.MenuItems().Select(static item => item.Text)];

    private sealed class RecordingInbox : IEngineInbox
    {
        public List<EngineEvent> Posted { get; } = [];

        public bool Post(EngineEvent engineEvent)
        {
            Posted.Add(engineEvent);
            return true;
        }
    }

    private sealed class CountingForeground : IForegroundOrchestrator
    {
        public ForegroundSnapshot Current => ForegroundSnapshot.Empty;

        public int Requests { get; private set; }

        public ValueTask<LeaseResult> AcquireAsync(
            LeaseRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            return ValueTask.FromResult<LeaseResult>(
                new LeaseResult.Denied(ForegroundDenialReason.RightsRefused)
            );
        }
    }

    /// <summary>Formats a message as its key: the tests read which text an entry shows.</summary>
    private sealed class KeyNames : ILocalizationContext, ILocalizer
    {
        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged
        {
            add { }
            remove { }
        }

        public ILocalizer Current => this;

        public ImmutableArray<LocaleInfo> Languages => [];

        public LocaleInfo Locale => throw new NotSupportedException();

        public bool TrySetLanguage(string code) => false;

        public string Format(Message message) => message.Key.ToString();

        public bool Contains(MessageKey key) => true;
    }
}
