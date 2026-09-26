using System.Collections.Immutable;
using System.Reflection;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// Documents for the save scheduler and the import planner. The library is built with
/// <c>ShortcutLibrary.CreateValidated</c> when the domain package has implemented it, and through its private
/// constructor until then, so the scheduler tests (which never look inside the library) already run.
/// </summary>
internal static class PersistenceDocuments
{
    public static UserSettings Settings(bool autoBackup = true) =>
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
            LastProfile = null,
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
            PanelPositions = [],
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
            Reliability = new ReliabilitySettings(true, autoBackup, true, true, false),
            Updates = new UpdateSettings(true, true, true, UpdateChannel.Stable),
            NoKeyboardUser = true,
        };

    /// <summary>A document whose version is <paramref name="marker"/> (in the General profile name).</summary>
    public static UserDocument Document(
        int marker,
        long epoch = 0,
        int marks = 0,
        bool autoBackup = true
    ) =>
        new(
            marker,
            Library([], [Profile("general", [], "General " + marker)]),
            new FrequentsState([], [], epoch, Usage(marks)),
            DuplicatePolicy.Empty,
            Settings(autoBackup),
            new OnboardingState(true)
        );

    public static UsageHistory Usage(int marks) =>
        marks == 0
            ? UsageHistory.Empty
            : new UsageHistory(
                ImmutableDictionary<ShortcutId, ValueList<DateTimeOffset>>.Empty.Add(
                    new ShortcutId("copy"),
                    ValueListBuilder.From(
                        Enumerable
                            .Range(0, marks)
                            .Select(i => DateTimeOffset.UnixEpoch.AddMinutes(i))
                    )
                )
            );

    public static Shortcut Shortcut(string id, string name = "x") =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            false,
            new CategoryId("edit"),
            new SystemAction(new SystemCommandId("lock")),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );

    public static Profile Profile(
        string id,
        IEnumerable<Shortcut> shortcuts,
        string? name = null,
        params string[] processes
    ) =>
        new(
            new ProfileId(id),
            LocalizedText.Same(name ?? id, LangCode.Es, LangCode.En),
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

    public static ShortcutLibrary Library(
        IEnumerable<Shortcut> always,
        IEnumerable<Profile> profiles
    )
    {
        var alwaysList = ValueListBuilder.From(always);
        var profileList = ValueListBuilder.From(profiles);
        try
        {
            return ShortcutLibrary.CreateValidated(alwaysList, profileList).Value;
        }
        catch (NotImplementedException)
        {
            return (ShortcutLibrary)
                typeof(ShortcutLibrary)
                    .GetConstructor(
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        [typeof(ValueList<Shortcut>), typeof(ValueList<Profile>)]
                    )!
                    .Invoke([alwaysList, profileList]);
        }
    }
}
