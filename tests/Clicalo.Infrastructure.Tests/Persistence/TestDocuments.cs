using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Documents for persistence tests, built through the Domain (<c>ShortcutLibrary.CreateValidated</c>,
/// <c>KeyChord.Create</c>).
/// </summary>
internal static class TestDocuments
{
    /// <summary>Settings with the docs/02 values, built by hand (no <c>SettingsSchema</c> needed).</summary>
    public static UserSettings Settings { get; } =
        new()
        {
            Language = LangCode.Es,
            Theme = ThemeChoice.Auto,
            Density = PanelDensity.Full,
            Size = Clicalo.Domain.Settings.PanelSize.Medium,
            Columns = 3,
            RowsPreference = 0,
            TextScalePercent = 100,
            Opacity = 0.92,
            AutoDim = true,
            DimTo = 0.35,
            ShowKeys = true,
            VoiceNumbers = false,
            StickyModifiersRow = false,
            ShowAlwaysVisibleRow = true,
            ShowProfileSelectorRow = true,
            ReduceMotion = false,
            Feedback = new FeedbackSettings(true, true),
            LockProfile = false,
            LastProfile = ProfileId.General,
            Dock = new DockSettings
            {
                Side = DockSide.Right,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = false,
                PerPage = 5,
                CoachDone = false,
            },
            PanelPositions = [new MonitorPosition(@"\\.\DISPLAY1", 1480, 40)],
            Touch = new TouchFilterSettings(
                "leve",
                TimeSpan.FromMilliseconds(300),
                14,
                35,
                TimeSpan.Zero
            ),
            KeySafety = new KeySafetySettings(TimeSpan.FromSeconds(60), true),
            AutoSuggestProfiles = true,
            Keyboard = new KeyboardSettings("es-LA", LangCode.Es, true),
            Ai = new AiSettings
            {
                Consent = false,
                Disabled = false,
                FreeLeftToday = 5,
                FreeResetAt = null,
                ApiKeyRef = null,
            },
            Reliability = new ReliabilitySettings(true, true, true, true, false),
            Updates = new UpdateSettings(true, true, true, UpdateChannel.Stable),
            NoKeyboardUser = true,
            HandlePositionsByMonitor =
            [
                new MonitorHandlePosition(Monitor, DockSide.Right, 30),
                new MonitorHandlePosition(Monitor, DockSide.Bottom, 72),
            ],
            ControlCenter = new ControlCenterPlacement(
                Monitor,
                120,
                80.5,
                1180,
                720,
                Maximized: false
            ),
            GlobalHotkey = new GlobalHotkeySettings(Enabled: true, "ctrl-alt-f10"),
            TimeMultiplier = 2,
        };

    /// <summary>A made-up stable monitor identifier (a DisplayConfig monitor device path).</summary>
    public const string Monitor =
        @"\\?\DISPLAY#GSM5B7F#5&2c8a1e3f&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    /// <summary>A chord in press order.</summary>
    /// <param name="keys">Key ids, optionally with <c>@left</c>/<c>@right</c>.</param>
    public static KeyChord Chord(params string[] keys)
    {
        var strokes = keys.Select(k =>
                k.EndsWith("@left", StringComparison.Ordinal)
                    ? new KeyStroke(new KeyId(k[..^5]), KeySide.Left)
                : k.EndsWith("@right", StringComparison.Ordinal)
                    ? new KeyStroke(new KeyId(k[..^6]), KeySide.Right)
                : new KeyStroke(new KeyId(k))
            )
            .ToList();
        return KeyChord.Create(strokes);
    }

    /// <summary>A shortcut.</summary>
    public static Shortcut Shortcut(string id, ShortcutAction action, bool isPrivate = false) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same("Atajo " + id, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            true,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), isPrivate),
            null,
            null
        );

    /// <summary>A Tap shortcut.</summary>
    public static Shortcut Tap(string id, params string[] keys) =>
        Shortcut(id, new TapAction(Chord(keys), []));

    /// <summary>A Text shortcut.</summary>
    public static Shortcut Text(string id, string text, bool isPrivate = true) =>
        Shortcut(id, new TextAction(SecretText.From(text), TextMethod.Unicode), isPrivate);

    /// <summary>A Web shortcut.</summary>
    public static Shortcut Url(string id, string url) =>
        Shortcut(id, new UrlAction(new UrlTarget.Valid(new Uri(url))));

    /// <summary>A profile.</summary>
    public static Profile Profile(
        string id,
        IEnumerable<Shortcut> shortcuts,
        params string[] processes
    ) =>
        new(
            new ProfileId(id),
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("apps"),
            false,
            processes.Length == 0
                ? new AppBinding.Manual()
                : new AppBinding.Processes(
                    ValueListBuilder.From(processes.Select(p => new ProcessName(p)))
                ),
            InjectionMode.VirtualKey,
            ValueListBuilder.From(shortcuts),
            null
        );

    /// <summary>A library, through <c>CreateValidated</c>.</summary>
    public static ShortcutLibrary Library(
        IEnumerable<Shortcut> always,
        IEnumerable<Profile> profiles
    )
    {
        var alwaysList = ValueListBuilder.From(always);
        var profileList = ValueListBuilder.From(profiles);
        return ShortcutLibrary.CreateValidated(alwaysList, profileList).Value;
    }

    /// <summary>A small document: two profiles, a secret text, a Web action and pins.</summary>
    /// <param name="marker">Changes the General profile name so versions differ.</param>
    public static UserDocument Document(int marker = 1) =>
        new(
            0,
            Library(
                [Tap("dict", "win", "h")],
                [
                    Profile(
                        "general",
                        [
                            Tap("copy", "ctrl", "c"),
                            Text("sig", "Saludos, M."),
                            Url("web", "https://example.org/a"),
                        ]
                    ) with
                    {
                        Name = LocalizedText.Same("General " + marker, LangCode.Es, LangCode.En),
                    },
                    Profile("word", [Tap("bold", "ctrl@left", "b")], "winword.exe"),
                ]
            ),
            new FrequentsState([new ShortcutId("copy")], [], 3, UsageHistory.Empty),
            DuplicatePolicy.Empty,
            Settings,
            new OnboardingState(true)
        );
}
