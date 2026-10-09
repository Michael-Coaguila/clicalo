using System.Globalization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using CsCheck;

namespace Clicalo.Domain.Tests.Generators;

/// <summary>
/// Every document command, built from a <see cref="CommandSeed"/> against the document it will apply to: existing and
/// missing shortcuts and profiles, every setting with valid and invalid values, blank drafts, macros and backups. The
/// product rules R4 and R7 and the store properties run these on generated documents.
/// </summary>
internal static class CommandFactory
{
    /// <summary>The command type each seed kind builds; a test checks it lists every command of the Domain.</summary>
    public static readonly Type[] Kinds =
    [
        typeof(CreateShortcut),
        typeof(DuplicateShortcut),
        typeof(EditShortcut),
        typeof(MoveShortcut),
        typeof(PinToAlwaysVisible),
        typeof(UnpinFromAlwaysVisible),
        typeof(DeleteShortcut),
        typeof(DiscardDraft),
        typeof(CreateProfile),
        typeof(EditProfile),
        typeof(MoveProfile),
        typeof(DeleteProfile),
        typeof(BindProcess),
        typeof(UnbindProcess),
        typeof(SetSetting),
        typeof(SetTouchFilter),
        typeof(SetPanelPosition),
        typeof(RecordUsage),
        typeof(PinToFrequents),
        typeof(UnpinFromFrequents),
        typeof(HideFromFrequents),
        typeof(ResetFrequents),
        typeof(MarkDuplicateAccepted),
        typeof(DeleteDuplicate),
        typeof(KeepOnlyInAlwaysVisible),
        typeof(DeleteMacroStep),
        typeof(ReplaceOnImport),
        typeof(RestoreBackup),
        typeof(FinishOnboarding),
    ];

    /// <summary>Any seed.</summary>
    public static readonly Gen<CommandSeed> Seed = Gen.Select(
        Gen.Int[0, Kinds.Length - 1],
        Gen.Int[0, 60],
        Gen.Int[0, 60],
        Gen.Int[0, 60],
        Gen.Bool,
        static (kind, a, b, c, flag) => new CommandSeed(kind, a, b, c, flag)
    );

    private static readonly ShortcutId Missing = new("gone");

    private static readonly ProfileId MissingProfile = new("ghost");

    private static readonly Shortcut[] Templates =
    [
        Template("", "", new TapAction(KeyChord.Empty, [])),
        Template("Copiar", "Copy", new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C), [])),
        Template("Mayús", "Shift", new HoldAction(KeyChord.FromKeys(KeyIds.Shift))),
        Template(
            "Macro",
            "Macro",
            new MacroAction([
                new KeysStep(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C)),
                new WaitStep(TimeSpan.FromMilliseconds(500)),
                new TextStep(SecretText.From("x")),
            ])
        ),
        Template("Firma", "Signature", new TextAction(SecretText.From("hola"), TextMethod.Unicode)),
        Template("copiar", "copy", new ToggleAction(KeyChord.FromKeys(KeyIds.C, KeyIds.Ctrl))),
        Template("Web", "Web", new UrlAction(new UrlTarget.Valid(new Uri("https://ejemplo.com")))),
        Template("Vacía", "Empty", new MacroAction([])),
    ];

    /// <summary>The command <paramref name="seed"/> stands for, against <paramref name="document"/>.</summary>
    public static IDocumentCommand Create(CommandSeed seed, UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(document);
        var library = document.Library;
        var shortcuts = library.EnumerateShortcuts().ToArray();
        var (a, b, c, flag) = (seed.A, seed.B, seed.C, seed.Flag);
        var target = PickShortcut(shortcuts, a);
        var kind = Kinds[Math.Abs(seed.Kind) % Kinds.Length];
        return kind.Name switch
        {
            nameof(CreateShortcut) => new CreateShortcut(
                PickList(library, a),
                TemplateAt(b),
                Position(c)
            ),
            nameof(DuplicateShortcut) => new DuplicateShortcut(target, TemplateAt(b).Name),
            nameof(EditShortcut) => new EditShortcut(Edited(library, target, b, flag)),
            nameof(MoveShortcut) => new MoveShortcut(target, PickList(library, b), Position(c)),
            nameof(PinToAlwaysVisible) => new PinToAlwaysVisible(target),
            nameof(UnpinFromAlwaysVisible) => new UnpinFromAlwaysVisible(
                PickAlways(shortcuts, a),
                PickProfile(library, b)
            ),
            nameof(DeleteShortcut) => new DeleteShortcut(target),
            nameof(DiscardDraft) => new DiscardDraft(flag ? PickBlankDraft(shortcuts, a) : target),
            nameof(CreateProfile) => new CreateProfile(ProfileTemplate(b, flag), Position(c)),
            nameof(EditProfile) => new EditProfile(EditedProfile(library, a, b, c, flag)),
            nameof(MoveProfile) => new MoveProfile(PickProfile(library, a), Position(b)),
            nameof(DeleteProfile) => new DeleteProfile(PickProfile(library, a)),
            nameof(BindProcess) => new BindProcess(PickProfile(library, a), Process(b), flag),
            nameof(UnbindProcess) => Unbind(library, a, b),
            nameof(SetSetting) => Setting(document, a, b, flag && c % 3 == 0),
            nameof(SetTouchFilter) => Touch(a, b, flag && c % 3 == 0),
            nameof(SetPanelPosition) => new SetPanelPosition(
                new MonitorPosition(
                    a % 3 == 0
                        ? string.Empty
                        : @"\\.\DISPLAY" + ((a % 2) + 1).ToString(CultureInfo.InvariantCulture),
                    b * 10,
                    c * 10
                )
            ),
            nameof(RecordUsage) => new RecordUsage(target),
            nameof(PinToFrequents) => new PinToFrequents(target),
            nameof(UnpinFromFrequents) => new UnpinFromFrequents(PickPin(document, a)),
            nameof(HideFromFrequents) => new HideFromFrequents(target),
            nameof(ResetFrequents) => new ResetFrequents(),
            nameof(MarkDuplicateAccepted) => new MarkDuplicateAccepted(KeyOf(library, target)),
            nameof(DeleteDuplicate) => new DeleteDuplicate(target),
            nameof(KeepOnlyInAlwaysVisible) => new KeepOnlyInAlwaysVisible(target),
            nameof(DeleteMacroStep) => new DeleteMacroStep(target, (c % 4) - 1),
            nameof(ReplaceOnImport) => new ReplaceOnImport(Imported(library, b)),
            nameof(RestoreBackup) => new RestoreBackup(Backup(document, b, c % 5 == 0)),
            _ => new FinishOnboarding(),
        };
    }

    /// <summary>The template of kind <paramref name="n"/>, blank draft first.</summary>
    public static Shortcut TemplateAt(int n) => Templates[Math.Abs(n) % Templates.Length];

    /// <summary>A minimal valid library: General with one shortcut.</summary>
    public static ShortcutLibrary MinimalLibrary() =>
        ShortcutLibrary
            .CreateValidated(
                [],
                [DomainGen.General(TemplateAt(1) with { Id = new ShortcutId("m1") })]
            )
            .Value;

    private static Shortcut Template(string es, string en, ShortcutAction action) =>
        new(
            new ShortcutId("_"),
            new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)]),
            new IconRef("bolt"),
            true,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );

    private static ShortcutId PickShortcut(LocatedShortcut[] shortcuts, int n)
    {
        var index = Math.Abs(n) % (shortcuts.Length + 1);
        return index == shortcuts.Length ? Missing : shortcuts[index].Shortcut.Id;
    }

    private static ShortcutId PickAlways(LocatedShortcut[] shortcuts, int n)
    {
        var always = shortcuts.Where(s => s.Location.List is ListRef.AlwaysVisible).ToArray();
        return always.Length == 0
            ? PickShortcut(shortcuts, n)
            : always[Math.Abs(n) % always.Length].Shortcut.Id;
    }

    private static ShortcutId PickBlankDraft(LocatedShortcut[] shortcuts, int n)
    {
        var drafts = shortcuts.Where(s => ShortcutCompleteness.IsBlankDraft(s.Shortcut)).ToArray();
        return drafts.Length == 0
            ? PickShortcut(shortcuts, n)
            : drafts[Math.Abs(n) % drafts.Length].Shortcut.Id;
    }

    private static ProfileId PickProfile(ShortcutLibrary library, int n)
    {
        var index = Math.Abs(n) % (library.Profiles.Count + 1);
        return index == library.Profiles.Count ? MissingProfile : library.Profiles[index].Id;
    }

    private static ShortcutId PickPin(UserDocument document, int n)
    {
        var pins = document.Frequents.Pins;
        return pins.IsEmpty ? Missing : pins[Math.Abs(n) % pins.Count];
    }

    private static ListRef PickList(ShortcutLibrary library, int n) =>
        n % 3 == 0
            ? new ListRef.AlwaysVisible()
            : new ListRef.InProfile(PickProfile(library, n / 3));

    private static ListPosition Position(int n) =>
        n % 4 == 0 ? ListPosition.End : ListPosition.At((n % 7) - 1);

    private static ProcessName Process(int n) =>
        DomainGen.Processes[Math.Abs(n) % DomainGen.Processes.Length];

    private static Shortcut Edited(ShortcutLibrary library, ShortcutId id, int n, bool blank)
    {
        if (!library.TryGetShortcut(id, out var shortcut))
        {
            return TemplateAt(n) with { Id = id };
        }

        var template = blank ? TemplateAt(0) : TemplateAt(n);
        return shortcut with { Name = template.Name, Action = template.Action };
    }

    private static Profile ProfileTemplate(int n, bool bound) =>
        new(
            new ProfileId("_"),
            LocalizedText.Same(
                "Plantilla " + (n % 5).ToString(CultureInfo.InvariantCulture),
                LangCode.Es,
                LangCode.En
            ),
            new IconRef("apps"),
            true,
            bound ? new AppBinding.Processes([Process(n)]) : new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [TemplateAt(n + 1), TemplateAt(n + 2)],
            null
        );

    private static Profile EditedProfile(
        ShortcutLibrary library,
        int a,
        int b,
        int c,
        bool blankName
    )
    {
        var id = PickProfile(library, a);
        var existing = library.TryGetProfile(id, out var profile)
            ? profile
            : ProfileTemplate(b, false) with
            {
                Id = id,
            };
        return existing with
        {
            Name =
                blankName && b % 2 == 0
                    ? LocalizedText.Same(" ", LangCode.Es, LangCode.En)
                    : LocalizedText.Same(
                        "Perfil " + (b % 4).ToString(CultureInfo.InvariantCulture),
                        LangCode.Es,
                        LangCode.En
                    ),
            Binding = b % 3 == 0 ? new AppBinding.Manual() : new AppBinding.Processes([Process(b)]),
            Injection = c % 2 == 0 ? InjectionMode.VirtualKey : InjectionMode.ScanCode,
        };
    }

    private static UnbindProcess Unbind(ShortcutLibrary library, int a, int b)
    {
        var id = PickProfile(library, a);
        var process =
            library.TryGetProfile(id, out var profile)
            && profile.Binding is AppBinding.Processes { Names.IsEmpty: false } bound
                ? bound.Names[Math.Abs(b) % bound.Names.Count]
                : Process(b);
        return new UnbindProcess(id, process);
    }

    private static CanonicalChord KeyOf(ShortcutLibrary library, ShortcutId id) =>
        library.TryGetShortcut(id, out var shortcut)
        && DuplicateIndex.TryGetKey(shortcut, out var key)
            ? key
        : CanonicalChord.TryFrom(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.X), out var fallback)
            ? fallback
        : default;

    private static ShortcutLibrary Imported(ShortcutLibrary library, int n)
    {
        var removable = library.Profiles.FirstOrDefault(p => p.Id != ProfileId.General);
        return n % 2 == 0 && removable is not null
            ? library.RemoveProfile(removable.Id).Value
            : MinimalLibrary();
    }

    private static UserDocument Backup(UserDocument document, int n, bool invalid)
    {
        var backup =
            n % 2 == 0
                ? UserDocument.Create(MinimalLibrary(), SettingsSchema.Defaults)
                : document with
                {
                    Settings = document.Settings with { Theme = (ThemeChoice)(n % 4) },
                    Revision = document.Revision + 7,
                };
        return invalid ? backup with { Settings = backup.Settings with { Columns = 9 } } : backup;
    }

    private static SetTouchFilter Touch(int a, int n, bool invalid) =>
        new(
            new TouchFilterSettings(
                new[] { "standard", "strong-tremor", SettingsSchema.PersonalTouchPreset }[a % 3],
                TimeSpan.FromMilliseconds(invalid ? 1050 : (n % 21) * 50),
                (n % 21) * 2,
                n % 81,
                TimeSpan.FromMilliseconds((n % 31) * 10)
            )
        );

    private static SetSetting Setting(UserDocument document, int a, int n, bool invalid)
    {
        var all = SettingsSchema.All;
        var descriptor = all[Math.Abs(a) % all.Length];
        return new SetSetting(
            descriptor.Path,
            invalid ? InvalidValue(descriptor, n) : ValidValue(descriptor, document, n)
        );
    }

    private static object? ValidValue(SettingDescriptor descriptor, UserDocument document, int n)
    {
        switch (descriptor.Path)
        {
            case SettingPaths.LastProfile:
                return n % 3 == 0 ? null : PickProfile(document.Library, n);
            case SettingPaths.MaxHold:
                return n % 4 == 3 ? null : SettingsSchema.MaxHoldChoices[n % 3];
            case SettingPaths.AiFreeResetAt:
                return n % 2 == 0 ? null : DomainGen.Now;
            case SettingPaths.AiApiKeyRef:
                return n % 2 == 0 ? null : "Clicalo/ai/test";
            case SettingPaths.PanelPositions:
                return n % 2 == 0
                    ? new ValueList<MonitorPosition>([])
                    : new ValueList<MonitorPosition>([new MonitorPosition(@"\\.\DISPLAY1", n, n)]);
            case SettingPaths.DockPerPage:
                return SettingsSchema.DockPerPageChoices[
                    n % SettingsSchema.DockPerPageChoices.Length
                ];
            case SettingPaths.TouchPreset:
                return new[] { "standard", "mild-tremor", SettingsSchema.PersonalTouchPreset }[
                    n % 3
                ];
            case SettingPaths.KeyboardLayout:
                return new[] { "es-LA", "en-US", string.Empty }[n % 3];
        }

        var range = descriptor.Range;
        return descriptor.Default switch
        {
            bool => n % 2 == 0,
            int when range is not null => (int)range.Clamp(range.Min + ((n % 5) * range.Step)),
            double when range is not null => range.Snap(range.Min + ((n % 5) * range.Step)),
            TimeSpan when range is not null => TimeSpan.FromMilliseconds(
                range.Min + ((n % 5) * range.Step)
            ),
            int value => value + (n % 2),
            LangCode => n % 2 == 0 ? LangCode.Es : LangCode.En,
            Enum value => Enum.GetValues(value.GetType())
                .GetValue(n % Enum.GetValues(value.GetType()).Length),
            _ => descriptor.Default,
        };
    }

    private static object? InvalidValue(SettingDescriptor descriptor, int n)
    {
        if (n % 2 == 0)
        {
            return new object();
        }

        var range = descriptor.Range;
        return descriptor.Default switch
        {
            int when range is not null => (int)(range.Max + range.Step),
            double when range is not null => range.Max + 1,
            TimeSpan when range is not null => TimeSpan.FromMilliseconds(range.Max + 1000),
            int => -1,
            Enum value => Enum.ToObject(value.GetType(), 999),
            LangCode => new LangCode(string.Empty),
            _ => new object(),
        };
    }
}
