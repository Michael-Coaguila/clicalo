using Clicalo.Application.Confirmation;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// Quick settings, edit mode, the context menu, test mode and «Reiniciar Frecuentes» headless: the document of the search
/// tests (Always visible «Dictar», General «Deshacer», Word «Negrita» and «Dictado», Navegador «Pestaña nueva»), a fake
/// clock, the real language files and recording fakes for the notice bar, the control center and the engine. The UI
/// thread is the test's own: <c>post</c> runs at once.
/// </summary>
internal sealed class InteractionsWorld
{
    public static readonly ShortcutId Dictate = new("dict");
    public static readonly ShortcutId Bold = new("bold");
    public static readonly ShortcutId Dictation = new("dictw");

    public InteractionsWorld(string language = "es", UserDocument? document = null)
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero));
        Store = new DocumentStore(
            document ?? SearchTestWorld.Document(),
            new SequentialTestIds(),
            new SearchTestWorld.NoBackups(),
            Time
        );
        Localization = PanelTestData.Localization(language);
        Confirm = new TwoStepConfirm(Time);
        TestMode = new TestModeViewModel(Engine, Localization, Notices, Time, Post);
        EditMode = new EditModeViewModel(
            Store,
            Confirm,
            Localization,
            Notices,
            ControlCenter,
            Time,
            Post
        );
        Menu = new TileContextMenuViewModel(Store, Localization, Notices, ControlCenter);
        QuickSettings = new QuickSettingsViewModel(
            Store,
            Localization,
            TestMode,
            ControlCenter,
            () => ProfileInView
        );
        Modes = new TileInteractionModes(EditMode, TestMode, Menu);
    }

    public FakeTimeProvider Time { get; }

    public DocumentStore Store { get; }

    public LocalizationContext Localization { get; }

    public TwoStepConfirm Confirm { get; }

    public PanelEngineInbox Engine { get; } = new();

    public RecordingNotices Notices { get; } = new();

    public RecordingControlCenter ControlCenter { get; } = new();

    public TestModeViewModel TestMode { get; }

    public EditModeViewModel EditMode { get; }

    public TileContextMenuViewModel Menu { get; }

    public QuickSettingsViewModel QuickSettings { get; }

    public TileInteractionModes Modes { get; }

    public ProfileId? ProfileInView { get; set; } = SearchTestWorld.Word;

    public UserSettings Settings => Store.Current.Settings;

    /// <summary>The UI thread of the tests is the test itself.</summary>
    public static void Post(Action action) => action();

    /// <summary>A tile of the panel for a shortcut of the document.</summary>
    public TileViewModel Tile(ShortcutId id, TileBehavior behavior = TileBehavior.Tap)
    {
        Store.Current.Library.TryGetShortcut(id, out var shortcut).ShouldBeTrue();
        var name = shortcut.Name.Get(LangCode.Es, LangCode.Es);
        return new TileViewModel(
            new TileModel(
                id,
                name,
                behavior,
                new TileBinding(shortcut, SearchTestWorld.Word, InjectionMode.VirtualKey),
                shortcut.Icon,
                shortcut.Category,
                string.Empty,
                string.Empty
            ),
            new PanelInteractionController(Engine, () => 1, Time)
        );
    }

    /// <summary>The text of a notice in the interface language.</summary>
    public string Text(PanelNotice notice) => Localization.Current.Format(notice.Text);

    /// <summary>Records what the panel would show in its notice bar.</summary>
    internal sealed class RecordingNotices : IPanelNoticeSink
    {
        public List<PanelNotice> Timed { get; } = [];

        public PanelNotice? Sticky { get; private set; }

        public object? StickyOwner { get; private set; }

        public void Notify(PanelNotice notice) => Timed.Add(notice);

        public void ShowSticky(object owner, PanelNotice notice)
        {
            StickyOwner = owner;
            Sticky = notice;
        }

        public void ClearSticky(object owner)
        {
            if (ReferenceEquals(owner, StickyOwner))
            {
                Sticky = null;
                StickyOwner = null;
            }
        }
    }

    /// <summary>Records what the panel asks of the control center.</summary>
    internal sealed class RecordingControlCenter : IControlCenterIntents
    {
        public List<string> Requests { get; } = [];

        public void OpenEditor(ShortcutId shortcut) => Requests.Add("editor:" + shortcut.Value);

        public void OpenLibrary(ProfileId profile) => Requests.Add("library:" + profile.Value);

        public void OpenControlCenter(ProfileId profile) => Requests.Add("cc:" + profile.Value);
    }

    /// <summary>A document whose Always visible row has <paramref name="count"/> shortcuts «x1», «x2»…</summary>
    public static UserDocument Crowded(int count) =>
        UserDocument.Create(
            ShortcutLibrary
                .CreateValidated(
                    [.. Enumerable.Range(1, count).Select(static i => Extra("x" + i))],
                    [
                        new Profile(
                            ProfileId.General,
                            LocalizedText.Same("General", LangCode.Es, LangCode.En),
                            new IconRef("apps"),
                            AutoIcon: false,
                            new AppBinding.Manual(),
                            InjectionMode.VirtualKey,
                            [],
                            Origin: null
                        ),
                    ]
                )
                .Value,
            SettingsSchema.Defaults
        );

    private static Shortcut Extra(string id) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            AutoIcon: false,
            new CategoryId("edit"),
            new TapAction(KeyChord.Empty, []),
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );
}
