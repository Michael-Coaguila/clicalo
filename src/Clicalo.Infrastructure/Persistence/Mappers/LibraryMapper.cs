using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Persistence.Dto;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// Profiles, shortcuts and actions ↔ their DTOs (blueprint §6.2, schema 1.0). Reading throws
/// <see cref="InvalidDataException"/> (or the Domain's <see cref="ArgumentException"/>) for what cannot be repaired; the
/// caller turns it into an invalid document. Writing reveals texts only to encrypt them (§6.7, D-10).
/// </summary>
internal static class LibraryMapper
{
    private const string MaxHoldInherit = "inherit";
    private const string MaxHoldNever = "never";
    private const string MaxHoldAfter = "after";

    // ------------------------------------------------------------------ reading

    /// <summary>Reads a profile and records what must be kept.</summary>
    /// <param name="dto">The profile.</param>
    /// <param name="keep">Collects unknown members and unavailable blobs.</param>
    public static Profile DecodeProfile(ProfileDto dto, PreservedCollector keep)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(keep);
        var id = Required(dto.Id, "profile.id");
        keep.Profile(id, dto.Extra);
        return new Profile(
            new ProfileId(id),
            Text(dto.Name),
            new IconRef(dto.Icon ?? string.Empty),
            dto.AutoIcon ?? false,
            dto.Processes is { } processes
                ? new AppBinding.Processes(
                    ValueListBuilder.From(processes.Select(p => new ProcessName(p)))
                )
                : new AppBinding.Manual(),
            dto.Compat == true ? InjectionMode.ScanCode : InjectionMode.VirtualKey,
            DecodeShortcuts(dto.Shortcuts, keep),
            Origin(dto.Origin)
        );
    }

    /// <summary>Reads a list of shortcuts.</summary>
    /// <param name="shortcuts">The shortcuts, or <see langword="null"/>.</param>
    /// <param name="keep">Collects unknown members and unavailable blobs.</param>
    public static ValueList<Shortcut> DecodeShortcuts(
        List<ShortcutDto>? shortcuts,
        PreservedCollector keep
    ) => ValueListBuilder.From((shortcuts ?? []).Select(s => DecodeShortcut(s, keep)));

    private static Shortcut DecodeShortcut(ShortcutDto dto, PreservedCollector keep)
    {
        var id = Required(dto.Id, "shortcut.id");
        var action = dto.Action ?? throw new InvalidDataException("shortcut.action");
        keep.Shortcut(id, dto.Extra, action.Type, action.Extra, action.Steps);
        return new Shortcut(
            new ShortcutId(id),
            Text(dto.Name),
            new IconRef(dto.Icon ?? string.Empty),
            dto.AutoIcon ?? false,
            new CategoryId(dto.Category ?? string.Empty),
            DecodeAction(id, action, keep),
            new ShortcutOptions(dto.Confirm ?? false, HoldLimitOf(dto), dto.Private ?? false),
            Origin(dto.Origin),
            dto.PinnedFrom is { Length: > 0 } from ? new ProfileId(from) : null
        );
    }

    private static HoldLimit HoldLimitOf(ShortcutDto dto) =>
        dto.MaxHold switch
        {
            null or MaxHoldInherit => new HoldLimit.InheritGlobal(),
            MaxHoldNever => new HoldLimit.Never(),
            MaxHoldAfter when dto.MaxHoldMs is { } ms && double.IsFinite(ms) && ms > 0 =>
                new HoldLimit.After(SettingsMapper.Milliseconds(ms)),
            _ => throw new InvalidDataException("shortcut.maxHold"),
        };

    private static ShortcutAction DecodeAction(string id, ActionDto dto, PreservedCollector keep)
    {
        if (!PersistedNames.Action.TryParse(dto.Type, out var kind))
        {
            throw new InvalidDataException("action.type");
        }

        return kind switch
        {
            ActionKind.Tap => new TapAction(
                Chord(dto.Keys),
                ValueListBuilder.From(
                    (dto.Variants ?? []).Select(v => new ChordVariant(
                        new LangCode(Required(v.Lang, "variant.lang")),
                        Chord(v.Keys)
                    ))
                )
            ),
            ActionKind.Hold => new HoldAction(Chord(dto.Keys)),
            ActionKind.Toggle => new ToggleAction(Chord(dto.Keys)),
            ActionKind.Text => new TextAction(
                keep.Text(id, -1, dto.Text),
                PersistedNames.TextMethod.ParseOr(dto.Method, TextMethod.Unicode)
            ),
            ActionKind.Mouse => new MouseAction(
                PersistedNames.Mouse.TryParse(dto.Mouse, out var op)
                    ? op
                    : throw new InvalidDataException("action.mouse"),
                PersistedNames.Speed.ParseOr(dto.Speed, ScrollSpeed.Normal)
            ),
            ActionKind.Macro => new MacroAction(
                ValueListBuilder.From(
                    (dto.Steps ?? []).Select((step, index) => DecodeStep(id, index, step, keep))
                )
            ),
            ActionKind.Url => new UrlAction(UrlTargetOf(dto)),
            ActionKind.App => new AppAction(AppTargetOf(dto.App)),
            ActionKind.System => new SystemAction(
                new SystemCommandId(Required(dto.Command, "action.command"))
            ),
            _ => throw new InvalidDataException("action.type"),
        };
    }

    private static MacroStep DecodeStep(
        string id,
        int index,
        StepDto step,
        PreservedCollector keep
    ) =>
        step.Kind switch
        {
            "keys" => new KeysStep(Chord(step.Keys)),
            "wait" => new WaitStep(
                SettingsMapper.Milliseconds(step.Ms ?? throw new InvalidDataException("step.ms"))
            ),
            "text" => new TextStep(keep.Text(id, index, step.Text)),
            "mouse" => new MouseStep(
                PersistedNames.Mouse.TryParse(step.Mouse, out var op)
                    ? op
                    : throw new InvalidDataException("step.mouse")
            ),
            _ => throw new InvalidDataException("step.kind"),
        };

    private static UrlTarget UrlTargetOf(ActionDto dto)
    {
        var text = dto.Url ?? string.Empty;
        return
            dto.Raw != true
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            )
            ? new UrlTarget.Valid(uri)
            : new UrlTarget.Raw(text);
    }

    private static AppTarget AppTargetOf(AppTargetDto? dto) =>
        dto?.Kind switch
        {
            "exe" => new AppTarget.Executable(
                Required(dto.Path, "app.path"),
                dto.Args ?? string.Empty
            ),
            "store" => new AppTarget.StoreApp(Required(dto.Aumid, "app.aumid")),
            "document" => new AppTarget.Document(Required(dto.Path, "app.path")),
            "raw" => new AppTarget.Raw(dto.Text ?? string.Empty),
            _ => throw new InvalidDataException("app.kind"),
        };

    private static KeyChord Chord(List<string>? keys)
    {
        var strokes = new List<KeyStroke>(keys?.Count ?? 0);
        foreach (var key in keys ?? [])
        {
            strokes.Add(
                KeyStrokeFormat.TryParse(key, out var stroke)
                    ? stroke
                    : throw new InvalidDataException("keys")
            );
        }

        return strokes.Count == 0 ? KeyChord.Empty : KeyChord.Create(strokes);
    }

    private static LocalizedText Text(Dictionary<string, string>? values) =>
        new(
            (values ?? []).Select(pair => new KeyValuePair<LangCode, string>(
                new LangCode(pair.Key),
                pair.Value
            ))
        );

    private static CatalogRef? Origin(CatalogRefDto? dto) =>
        dto is null
            ? null
            : new CatalogRef(
                dto.Source ?? string.Empty,
                dto.Version ?? string.Empty,
                dto.Item ?? string.Empty
            );

    private static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(what) : value;

    // ------------------------------------------------------------------ writing

    /// <summary>The persisted form of a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="keep">What was kept when reading.</param>
    /// <param name="texts">How texts are written.</param>
    public static ProfileDto EncodeProfile(Profile profile, PreservedFields keep, TextExport texts)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(keep);
        return new ProfileDto
        {
            Id = profile.Id.Value,
            Name = Names(profile.Name),
            Icon = profile.Icon.Name,
            AutoIcon = profile.AutoIcon,
            Processes = profile.Binding is AppBinding.Processes bound
                ? [.. bound.Names.Select(n => n.Value)]
                : null,
            Compat = profile.Injection == InjectionMode.ScanCode,
            Shortcuts = EncodeShortcuts(profile.Shortcuts, keep, texts),
            Origin = Origin(profile.Origin),
            Extra = Copy(keep.Profiles.GetValueOrDefault(profile.Id.Value)),
        };
    }

    /// <summary>The persisted form of a list of shortcuts.</summary>
    /// <param name="shortcuts">The shortcuts.</param>
    /// <param name="keep">What was kept when reading.</param>
    /// <param name="texts">How texts are written.</param>
    public static List<ShortcutDto> EncodeShortcuts(
        ValueList<Shortcut> shortcuts,
        PreservedFields keep,
        TextExport texts
    ) => [.. shortcuts.Select(s => EncodeShortcut(s, keep, texts))];

    private static ShortcutDto EncodeShortcut(
        Shortcut shortcut,
        PreservedFields keep,
        TextExport texts
    )
    {
        var id = shortcut.Id.Value;
        var extras = keep.Shortcuts.GetValueOrDefault(id);
        var (maxHold, maxHoldMs) = shortcut.Options.MaxHold switch
        {
            HoldLimit.Never => (MaxHoldNever, (double?)null),
            HoldLimit.After after => (MaxHoldAfter, after.Limit.TotalMilliseconds),
            _ => ((string?)null, (double?)null),
        };
        return new ShortcutDto
        {
            Id = id,
            Name = Names(shortcut.Name),
            Icon = shortcut.Icon.Name,
            AutoIcon = shortcut.AutoIcon,
            Category = shortcut.Category.Value,
            Action = EncodeAction(shortcut, extras, keep, texts),
            Confirm = shortcut.Options.Confirm,
            MaxHold = maxHold,
            MaxHoldMs = maxHoldMs,
            Private = shortcut.Options.IsPrivate,
            Origin = Origin(shortcut.Origin),
            PinnedFrom = shortcut.PinnedFrom?.Value,
            Extra = Copy(extras?.Shortcut),
        };
    }

    private static ActionDto EncodeAction(
        Shortcut shortcut,
        PreservedFields.ShortcutExtras? extras,
        PreservedFields keep,
        TextExport texts
    )
    {
        var type = PersistedNames.Action.Name(shortcut.Action.Kind);
        var sameType = string.Equals(extras?.ActionType, type, StringComparison.Ordinal);
        var id = shortcut.Id.Value;
        var isPrivate = shortcut.Options.IsPrivate;
        var dto = new ActionDto { Type = type, Extra = sameType ? Copy(extras!.Action) : null };
        return shortcut.Action switch
        {
            TapAction tap => dto with
            {
                Keys = Keys(tap.Chord),
                Variants = tap.Variants.IsEmpty
                    ? null
                    :
                    [
                        .. tap.Variants.Select(v => new VariantDto
                        {
                            Lang = v.AppsLanguage.Value,
                            Keys = Keys(v.Chord),
                        }),
                    ],
            },
            HoldAction hold => dto with { Keys = Keys(hold.Chord) },
            ToggleAction toggle => dto with { Keys = Keys(toggle.Chord) },
            TextAction text => dto with
            {
                Text = TextNode(
                    text.Text,
                    new PreservedFields.TextSlot(id, -1),
                    keep,
                    texts,
                    isPrivate
                ),
                Method = PersistedNames.TextMethod.Name(text.Method),
            },
            MouseAction mouse => dto with
            {
                Mouse = PersistedNames.Mouse.Name(mouse.Op),
                Speed = PersistedNames.Speed.Name(mouse.Speed),
            },
            MacroAction macro => dto with
            {
                Steps =
                [
                    .. macro.Steps.Select(
                        (step, index) =>
                            EncodeStep(
                                step,
                                index,
                                id,
                                sameType ? extras : null,
                                keep,
                                texts,
                                isPrivate
                            )
                    ),
                ],
            },
            UrlAction url => url.Target switch
            {
                UrlTarget.Valid valid => dto with { Url = valid.Address.OriginalString },
                UrlTarget.Raw raw => dto with { Url = raw.Text, Raw = true },
                _ => throw new InvalidOperationException("Unknown URL target."),
            },
            AppAction app => dto with { App = AppTarget(app.Target) },
            SystemAction system => dto with { Command = system.Command.Value },
            _ => throw new InvalidOperationException("Unknown action type."),
        };
    }

    private static StepDto EncodeStep(
        MacroStep step,
        int index,
        string id,
        PreservedFields.ShortcutExtras? extras,
        PreservedFields keep,
        TextExport texts,
        bool isPrivate
    )
    {
        var dto = step switch
        {
            KeysStep keys => new StepDto { Kind = "keys", Keys = Keys(keys.Chord) },
            WaitStep wait => new StepDto { Kind = "wait", Ms = wait.Duration.TotalMilliseconds },
            TextStep text => new StepDto
            {
                Kind = "text",
                Text = TextNode(
                    text.Text,
                    new PreservedFields.TextSlot(id, index),
                    keep,
                    texts,
                    isPrivate
                ),
            },
            MouseStep mouse => new StepDto
            {
                Kind = "mouse",
                Mouse = PersistedNames.Mouse.Name(mouse.Op),
            },
            _ => throw new InvalidOperationException("Unknown macro step."),
        };
        if (extras is not null && index < extras.Steps.Length)
        {
            var (kind, extra) = extras.Steps[index];
            if (string.Equals(kind, dto.Kind, StringComparison.Ordinal))
            {
                dto = dto with { Extra = Copy(extra) };
            }
        }

        return dto;
    }

    private static JsonNode TextNode(
        SecretText text,
        PreservedFields.TextSlot slot,
        PreservedFields keep,
        TextExport texts,
        bool isPrivate
    )
    {
        if (
            !text.IsAvailable
            && texts == TextExport.Encrypt
            && keep.Texts.TryGetValue(slot, out var original)
        )
        {
            return JsonNode.Parse(original.GetRawText())!;
        }

        return texts switch
        {
            TextExport.Plain => SecretTextCodec.Reveal(text, isPrivate),
            TextExport.Exclude => SecretTextCodec.Exclude(text, isPrivate),
            _ => SecretTextCodec.Protect(text, isPrivate),
        };
    }

    private static AppTargetDto AppTarget(AppTarget target) =>
        target switch
        {
            Domain.Library.AppTarget.Executable exe => new AppTargetDto
            {
                Kind = "exe",
                Path = exe.Path,
                Args = exe.Arguments.Length == 0 ? null : exe.Arguments,
            },
            Domain.Library.AppTarget.StoreApp store => new AppTargetDto
            {
                Kind = "store",
                Aumid = store.AppUserModelId,
            },
            Domain.Library.AppTarget.Document document => new AppTargetDto
            {
                Kind = "document",
                Path = document.Path,
            },
            Domain.Library.AppTarget.Raw raw => new AppTargetDto { Kind = "raw", Text = raw.Text },
            _ => throw new InvalidOperationException("Unknown app target."),
        };

    private static List<string> Keys(KeyChord chord) =>
        [.. chord.Strokes.Select(KeyStrokeFormat.Format)];

    private static Dictionary<string, string> Names(LocalizedText text) =>
        text.Values.ToDictionary(
            pair => pair.Key.Value,
            pair => pair.Value,
            StringComparer.Ordinal
        );

    private static CatalogRefDto? Origin(CatalogRef? origin) =>
        origin is { } o
            ? new CatalogRefDto
            {
                Source = o.Source,
                Version = o.Version,
                Item = o.ItemId,
            }
            : null;

    /// <summary>A mutable copy for a DTO, or <see langword="null"/>.</summary>
    /// <param name="extra">Kept members.</param>
    public static Dictionary<string, JsonElement>? Copy(
        IReadOnlyDictionary<string, JsonElement>? extra
    ) =>
        extra is null || extra.Count == 0
            ? null
            : new Dictionary<string, JsonElement>(extra, StringComparer.Ordinal);

    /// <summary>Collects, while reading, what a rewrite must keep.</summary>
    internal sealed class PreservedCollector
    {
        private readonly ImmutableDictionary<
            string,
            IReadOnlyDictionary<string, JsonElement>
        >.Builder _profiles = ImmutableDictionary.CreateBuilder<
            string,
            IReadOnlyDictionary<string, JsonElement>
        >(StringComparer.Ordinal);

        private readonly ImmutableDictionary<
            string,
            PreservedFields.ShortcutExtras
        >.Builder _shortcuts = ImmutableDictionary.CreateBuilder<
            string,
            PreservedFields.ShortcutExtras
        >(StringComparer.Ordinal);

        private readonly ImmutableDictionary<PreservedFields.TextSlot, JsonElement>.Builder _texts =
            ImmutableDictionary.CreateBuilder<PreservedFields.TextSlot, JsonElement>();

        /// <summary>Texts that could not be decrypted here.</summary>
        public int UnavailableTexts { get; private set; }

        public void Profile(string id, Dictionary<string, JsonElement>? extra)
        {
            if (extra is { Count: > 0 })
            {
                _profiles[id] = extra;
            }
        }

        public void Shortcut(
            string id,
            Dictionary<string, JsonElement>? extra,
            string? actionType,
            Dictionary<string, JsonElement>? actionExtra,
            List<StepDto>? steps
        )
        {
            var stepExtras = (steps ?? [])
                .Select(s => (s.Kind, (IReadOnlyDictionary<string, JsonElement>?)s.Extra))
                .ToImmutableArray();
            if (
                extra is { Count: > 0 }
                || actionExtra is { Count: > 0 }
                || stepExtras.Any(s => s.Item2 is { Count: > 0 })
            )
            {
                _shortcuts[id] = new PreservedFields.ShortcutExtras(
                    extra,
                    actionType,
                    actionExtra,
                    stepExtras
                );
            }
        }

        public SecretText Text(string id, int step, JsonNode? node)
        {
            var text = SecretTextCodec.Unprotect(node);
            if (!text.IsAvailable)
            {
                UnavailableTexts++;
                if (SecretTextCodec.IsEncrypted(node))
                {
                    using var original = JsonDocument.Parse(node!.ToJsonString());
                    _texts[new PreservedFields.TextSlot(id, step)] = original.RootElement.Clone();
                }
            }

            return text;
        }

        public PreservedFields Build(PayloadDto payload) =>
            new()
            {
                Root = payload.Extra,
                Settings = payload.Settings?.Extra,
                Always = payload.Always?.Extra,
                Frequents = payload.Frequents?.Extra,
                Onboarding = payload.Onboarding?.Extra,
                Profiles = _profiles.ToImmutable(),
                Shortcuts = _shortcuts.ToImmutable(),
                Texts = _texts.ToImmutable(),
            };
    }
}
