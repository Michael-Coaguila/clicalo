using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using CsCheck;

namespace Clicalo.Domain.Tests.Generators;

/// <summary>
/// CsCheck generators of the domain model: key chords, the nine action kinds, shortcuts, profiles, valid libraries and
/// whole user documents. They use the public API only, because Application.Tests compiles this folder too. Names,
/// chords and processes come from small pools on purpose, so repetitions, same names and shared processes are common.
/// </summary>
internal static class DomainGen
{
    /// <summary>The «now» of every generated document.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Processes the generated profiles bind to, with case variants of the same executable.</summary>
    public static readonly ProcessName[] Processes =
    [
        new("winword.exe"),
        new("WINWORD.EXE"),
        new("chrome.exe"),
        new("code.exe"),
        new("excel.exe"),
        new("notepad.exe"),
    ];

    private static readonly KeyId[] CatalogKeys = [.. KeyDefinitions.All.Select(d => d.Id)];

    private static readonly KeyId[] OtherKeys =
    [
        new("char:ñ"),
        new("char:Ñ"),
        new("char:+"),
        new("char:%"),
        new("lwin"),
        new("x.custom+key"),
    ];

    private static readonly string[] SpanishNames =
    [
        "Copiar",
        "copiar ",
        "Pegar",
        "Guardar",
        "",
        " ",
    ];

    private static readonly string[] EnglishNames = ["Copy", "Paste", "Save", "Bold", ""];

    /// <summary>A few chords that repeat across the generated shortcuts (order and side variants included).</summary>
    private static readonly KeyChord[] CommonChords =
    [
        KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C),
        KeyChord.FromKeys(KeyIds.C, KeyIds.Ctrl),
        KeyChord.FromKeys(KeyIds.LeftCtrl, KeyIds.C),
        KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift, KeyIds.S),
        KeyChord.FromKeys(KeyIds.Shift, KeyIds.Ctrl, KeyIds.S),
        KeyChord.FromKeys(KeyIds.AltGr),
        KeyChord.Empty,
    ];

    /// <summary>Any key: catalog keys, characters and keys outside the catalog.</summary>
    public static readonly Gen<KeyId> Key = Gen.Frequency(
        (6, Gen.OneOfConst(CatalogKeys)),
        (1, Gen.OneOfConst(OtherKeys))
    );

    /// <summary>Any stroke, with any side (Create normalizes it).</summary>
    public static readonly Gen<KeyStroke> Stroke = Gen.Select(
        Key,
        Gen.Enum<KeySide>(),
        static (key, side) => new KeyStroke(key, side)
    );

    /// <summary>Any normalized chord, empty included.</summary>
    public static readonly Gen<KeyChord> Chord = Stroke.Array[0, 5].Select(KeyChord.Create);

    /// <summary>Chords for shortcuts: common ones half of the time, so repetitions happen.</summary>
    public static readonly Gen<KeyChord> ShortcutChord = Gen.Frequency(
        (1, Gen.OneOfConst(CommonChords)),
        (1, Chord)
    );

    /// <summary>A name in Spanish and English from small pools, blank names included.</summary>
    public static readonly Gen<LocalizedText> Name = Gen.Select(
        Gen.OneOfConst(SpanishNames),
        Gen.OneOfConst(EnglishNames),
        static (es, en) => new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)])
    );

    /// <summary>A name with text in at least one language (profiles).</summary>
    public static readonly Gen<LocalizedText> ProfileName = Gen.Select(
        Gen.OneOfConst("Word", "Navegador", "Código", "General 2"),
        static name => LocalizedText.Same(name, LangCode.Es, LangCode.En)
    );

    /// <summary>Texts of Text actions and text steps: empty, unavailable (COP-005) or a secret.</summary>
    public static readonly Gen<SecretText> Secret = Gen.Frequency(
        (1, Gen.Const(SecretText.Empty)),
        (1, Gen.Const(SecretText.Unavailable)),
        (4, Gen.OneOfConst("hola", "ñandú 🙂", "a\nb").Select(static t => SecretText.From(t)))
    );

    /// <summary>Any macro step; waits are always inside the range (I6).</summary>
    public static readonly Gen<MacroStep> Step = Gen.OneOf<MacroStep>(
        ShortcutChord.Select(static c => (MacroStep)new KeysStep(c)),
        Gen.Int[1, 100]
            .Select(static n => (MacroStep)new WaitStep(TimeSpan.FromMilliseconds(n * 100))),
        Secret.Select(static t => (MacroStep)new TextStep(t)),
        Gen.Enum<MouseOp>().Select(static op => (MacroStep)new MouseStep(op))
    );

    /// <summary>Any of the nine action kinds, complete or not.</summary>
    public static readonly Gen<ShortcutAction> Action = Gen.Frequency<ShortcutAction>(
        (
            4,
            Gen.Select(
                ShortcutChord,
                Gen.Bool,
                static (chord, variant) =>
                    (ShortcutAction)
                        new TapAction(chord, variant ? [new ChordVariant(LangCode.En, chord)] : [])
            )
        ),
        (2, ShortcutChord.Select(static c => (ShortcutAction)new HoldAction(c))),
        (2, ShortcutChord.Select(static c => (ShortcutAction)new ToggleAction(c))),
        (
            1,
            Gen.Select(
                Secret,
                Gen.Enum<TextMethod>(),
                static (text, method) => (ShortcutAction)new TextAction(text, method)
            )
        ),
        (
            1,
            Gen.Select(
                Gen.Enum<MouseOp>(),
                Gen.Enum<ScrollSpeed>(),
                static (op, speed) => (ShortcutAction)new MouseAction(op, speed)
            )
        ),
        (1, Step.Array[0, 3].Select(static s => (ShortcutAction)new MacroAction([.. s]))),
        (
            1,
            Gen.OneOfConst<ShortcutAction>(
                new UrlAction(new UrlTarget.Valid(new Uri("https://ejemplo.com/a?b=1"))),
                new UrlAction(new UrlTarget.Valid(new Uri("ftp://ejemplo.com"))),
                new UrlAction(new UrlTarget.Raw("ejemplo")),
                new UrlAction(new UrlTarget.Raw(""))
            )
        ),
        (
            1,
            Gen.OneOfConst<ShortcutAction>(
                new AppAction(new AppTarget.Executable("notepad.exe", "")),
                new AppAction(
                    new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
                ),
                new AppAction(new AppTarget.Document(@"C:\datos\a.txt")),
                new AppAction(new AppTarget.Raw("cmd /c del")),
                new AppAction(new AppTarget.Executable(" ", ""))
            )
        ),
        (1, Gen.Const<ShortcutAction>(new SystemAction(new SystemCommandId("lock"))))
    );

    /// <summary>Any options, with every hold limit.</summary>
    public static readonly Gen<ShortcutOptions> Options = Gen.Select(
        Gen.Bool,
        Gen.OneOfConst<HoldLimit>(
            new HoldLimit.InheritGlobal(),
            new HoldLimit.Never(),
            new HoldLimit.After(TimeSpan.FromSeconds(30))
        ),
        Gen.Bool,
        static (confirm, limit, isPrivate) => new ShortcutOptions(confirm, limit, isPrivate)
    );

    /// <summary>A blank draft (ATJ-011): no name, a Tap without keys.</summary>
    public static readonly Shortcut BlankDraft = new(
        new ShortcutId("_"),
        new LocalizedText([new(LangCode.Es, string.Empty), new(LangCode.En, string.Empty)]),
        new IconRef("bolt"),
        true,
        new CategoryId("edit"),
        new TapAction(KeyChord.Empty, []),
        new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
        null,
        null
    );

    /// <summary>A shortcut without its final id (see <see cref="WithId"/>); one in ten is a blank draft.</summary>
    public static readonly Gen<Shortcut> ShortcutContent = Gen.Frequency(
        (1, Gen.Const(BlankDraft)),
        (9, AnyShortcutContent())
    );

    private static Gen<Shortcut> AnyShortcutContent() =>
        Gen.Select(
            Name,
            Gen.OneOfConst(new IconRef("bolt"), new IconRef("content_copy")),
            Gen.Bool,
            Gen.OneOfConst(new CategoryId("edit"), new CategoryId("fmt")),
            Action,
            Options,
            static (name, icon, autoIcon, category, action, options) =>
                new Shortcut(
                    new ShortcutId("_"),
                    name,
                    icon,
                    autoIcon,
                    category,
                    action,
                    options,
                    null,
                    null
                )
        );

    /// <summary>A profile without its final ids and before its processes are made unique.</summary>
    public static readonly Gen<Profile> ProfileContent = Gen.Select(
        ProfileName,
        Gen.OneOfConst(Processes).Array[0, 2],
        Gen.Enum<InjectionMode>(),
        ShortcutContent.Array[0, 5],
        static (name, processes, injection, shortcuts) =>
            new Profile(
                new ProfileId("_"),
                name,
                new IconRef("apps"),
                true,
                processes.Length == 0
                    ? new AppBinding.Manual()
                    : new AppBinding.Processes([.. processes]),
                injection,
                [.. shortcuts],
                null
            )
    );

    /// <summary>A valid library: General plus up to four profiles, and up to four shortcuts in Always visible.</summary>
    public static readonly Gen<ShortcutLibrary> Library = Gen.Select(
        ShortcutContent.Array[0, 4],
        ShortcutContent.Array[0, 5],
        ProfileContent.Array[0, 4],
        static (always, general, profiles) => BuildLibrary(always, general, profiles)
    );

    /// <summary>Settings: the defaults with a few leaves changed inside their domains.</summary>
    public static readonly Gen<UserSettings> Settings = Gen.Select(
        Gen.Enum<ThemeChoice>(),
        Gen.Int[2, 4],
        Gen.Int[6, 20],
        Gen.Bool,
        Gen.Bool,
        Gen.OneOfConst<TimeSpan?>(null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(120)),
        Gen.Bool,
        static (theme, columns, opacity, lockProfile, stripRow, maxHold, safeSwitch) =>
            SettingsSchema.Defaults with
            {
                Theme = theme,
                Columns = columns,
                Opacity = SettingsSchema.Opacity.Snap(opacity * 0.05),
                LockProfile = lockProfile,
                ShowAlwaysVisibleRow = stripRow,
                KeySafety = new KeySafetySettings(maxHold, safeSwitch),
            }
    );

    /// <summary>A valid user document: every invariant holds, with dangling pins and old usage on purpose.</summary>
    public static readonly Gen<UserDocument> Document = Library.SelectMany(static library =>
    {
        var shortcuts = library.EnumerateShortcuts().Select(s => s.Shortcut).ToArray();
        var ids = shortcuts.Select(s => s.Id).Append(new ShortcutId("gone")).ToArray();
        var keys = shortcuts
            .Select(s => DuplicateIndex.TryGetKey(s, out var key) ? (CanonicalChord?)key : null)
            .OfType<CanonicalChord>()
            .Distinct()
            .ToArray();
        var id = Gen.OneOfConst(ids);
        var ignored =
            keys.Length == 0
                ? Gen.Const(Array.Empty<CanonicalChord>())
                : Gen.OneOfConst(keys).Array[0, 2];
        var lastProfile = Gen.Frequency<ProfileId?>(
            (1, Gen.OneOfConst<ProfileId?>(null, null)),
            (3, Gen.OneOfConst([.. library.Profiles.Select(p => (ProfileId?)p.Id)]))
        );
        return Gen.Select(
            Settings,
            id.Array[0, 4],
            id.Array[0, 3],
            Gen.Select(id, Gen.Int[0, 40]).Array[0, 12],
            lastProfile,
            ignored,
            Gen.Int[0, 3],
            Gen.Bool,
            (settings, pins, hidden, marks, last, dup, epoch, onboarded) =>
                new UserDocument(
                    0,
                    library,
                    new FrequentsState(
                        [.. pins.Distinct()],
                        [.. hidden.Distinct().OrderBy(h => h.Value, StringComparer.Ordinal)],
                        epoch,
                        Usage(marks)
                    ),
                    new DuplicatePolicy([.. dup.Distinct()]),
                    settings with
                    {
                        LastProfile = last,
                    },
                    new OnboardingState(onboarded)
                )
        );
    });

    /// <summary>A shortcut content with its final id.</summary>
    public static Shortcut WithId(Shortcut content, string id) =>
        content with
        {
            Id = new ShortcutId(id),
        };

    /// <summary>A usage history with the given marks, <c>(shortcut, days before <see cref="Now"/>)</c>, oldest first.</summary>
    public static UsageHistory Usage(IEnumerable<(ShortcutId Id, int DaysAgo)> marks) =>
        new(
            marks
                .GroupBy(m => m.Id)
                .ToImmutableDictionary(
                    g => g.Key,
                    g => new ValueList<DateTimeOffset>([
                        .. g.Select(m => Now.AddDays(-m.DaysAgo)).Order(),
                    ])
                )
        );

    /// <summary>General with its fixed id, no process and the given shortcuts.</summary>
    public static Profile General(params ReadOnlySpan<Shortcut> shortcuts) =>
        new(
            ProfileId.General,
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            true,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            null
        );

    private static ShortcutLibrary BuildLibrary(
        Shortcut[] always,
        Shortcut[] general,
        Profile[] profiles
    )
    {
        var next = 0;
        string NextId(string prefix) =>
            prefix + (++next).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Shortcut[] Assign(Shortcut[] items) => [.. items.Select(s => WithId(s, NextId("s")))];

        var owned = new HashSet<ProcessName>();
        var built = new List<Profile> { General(Assign(general)) };
        foreach (var profile in profiles)
        {
            var names = profile.Binding is AppBinding.Processes p
                ? p.Names.Where(owned.Add).ToArray()
                : [];
            built.Add(
                profile with
                {
                    Id = new ProfileId(NextId("p")),
                    Binding =
                        names.Length == 0
                            ? new AppBinding.Manual()
                            : new AppBinding.Processes([.. names]),
                    Shortcuts = [.. Assign([.. profile.Shortcuts])],
                }
            );
        }

        var result = ShortcutLibrary.CreateValidated([.. Assign(always)], [.. built]);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                "Generated an invalid library: " + result.Failure.Code
            );
    }
}
