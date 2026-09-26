using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Builds the Clícalo document of a <see cref="V1Plan"/> (catalog §7.4): new opaque ids in order, names equal in ES and
/// EN, icons that follow the name, compatible mode off, the library through <see cref="ShortcutLibrary.CreateValidated"/>,
/// the repeated keys of the import in «It's fine» (MIG-008), and the settings v1 has on top of a new installation's.
/// Fails, writing nothing, if an invariant breaks or a count differs (MIG-004).
/// </summary>
internal static class V1DocumentBuilder
{
    /// <summary>The icon of a new profile without suggestion (EDI-005).</summary>
    public static IconRef ProfileFallbackIcon { get; } = new("apps");

    /// <summary>The icon of a new shortcut without suggestion (EDI-005).</summary>
    public static IconRef ShortcutFallbackIcon { get; } = new("bolt");

    /// <summary>Builds the document and its report.</summary>
    /// <param name="plan">What the conversion decided.</param>
    /// <param name="context">Ids, the new installation's document and the icon suggestion.</param>
    public static Result<V1Conversion> Build(V1Plan plan, V1ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);
        var baseline = context.Baseline;
        var ids = new ProfileId[plan.Profiles.Count];
        var profiles = ImmutableArray.CreateBuilder<Profile>(plan.Profiles.Count);
        for (var i = 0; i < plan.Profiles.Count; i++)
        {
            var planned = plan.Profiles[i];
            ids[i] = planned.IsGeneral ? ProfileId.General : context.Ids.NewProfileId();
            profiles.Add(BuildProfile(planned, ids[i], baseline.Library.General, context));
        }

        var created = ShortcutLibrary.CreateValidated(
            [],
            new ValueList<Profile>(profiles.MoveToImmutable())
        );
        if (!created.TryGetValue(out var library))
        {
            return Results.Fail<V1Conversion>(V1Failures.Invalid);
        }

        var (ignored, duplicateNotes) = V1DuplicateScan.Find(library);
        var settings = baseline.Settings with
        {
            Opacity = plan.Settings.Opacity,
            Size = plan.Settings.Size,
            LastProfile = ids[plan.Settings.LastProfile],
            PanelPositions = plan.Settings.Position is { } position
                ? [position]
                : baseline.Settings.PanelPositions,
        };
        var document = baseline with
        {
            Library = library,
            Frequents = FrequentsState.Empty,
            Duplicates = new DuplicatePolicy(ignored),
            Settings = settings,
        };
        if (!document.Validate().IsEmpty)
        {
            return Results.Fail<V1Conversion>(V1Failures.Invalid);
        }

        var notes = new ValueList<MigrationNote>([.. plan.Notes, .. duplicateNotes]);
        var output = Count(library, plan, notes);
        if (output != plan.Input)
        {
            return Results.Fail<V1Conversion>(V1Failures.CountMismatch);
        }

        return Results.Ok(
            new V1Conversion(document, new MigrationReport(plan.Input, output, notes))
        );
    }

    private static Profile BuildProfile(
        V1ProfilePlan planned,
        ProfileId id,
        Profile general,
        V1ConversionContext context
    )
    {
        var shortcuts = ImmutableArray.CreateBuilder<Shortcut>(planned.Shortcuts.Count);
        foreach (var shortcut in planned.Shortcuts)
        {
            shortcuts.Add(BuildShortcut(shortcut, context));
        }

        var list = new ValueList<Shortcut>(shortcuts.MoveToImmutable());
        if (planned.IsGeneral)
        {
            // General keeps the name and icon of a new installation; v1 General is manual (I4).
            return general with { Shortcuts = list, Binding = new AppBinding.Manual() };
        }

        AppBinding binding = planned.Process is { } process
            ? new AppBinding.Processes([new ProcessName(process)])
            : new AppBinding.Manual();
        return new Profile(
            id,
            LocalizedText.Same(planned.Name, LangCode.Es, LangCode.En),
            context.SuggestIcon?.Invoke(planned.Name, null) ?? ProfileFallbackIcon,
            AutoIcon: true,
            binding,
            InjectionMode.VirtualKey,
            list,
            Origin: null
        );
    }

    private static Shortcut BuildShortcut(V1ShortcutPlan planned, V1ConversionContext context)
    {
        KeyChord? suggestionKeys = null;
        ShortcutAction action;
        IconRef? fixedIcon = null;
        switch (planned.Action)
        {
            case V1ActionPlan.KeyPresses { Chords.Count: 0 }:
                action = new TapAction(KeyChord.Empty, []);
                break;
            case V1ActionPlan.KeyPresses { Chords.Count: 1 } keys:
                suggestionKeys = KeyChord.Create(keys.Chords[0]);
                action = new TapAction(suggestionKeys, []);
                break;
            case V1ActionPlan.KeyPresses keys:
                var steps = ImmutableArray.CreateBuilder<MacroStep>(keys.Chords.Count);
                foreach (var chord in keys.Chords)
                {
                    steps.Add(new KeysStep(KeyChord.Create(chord)));
                }

                action = new MacroAction(new ValueList<MacroStep>(steps.MoveToImmutable()));
                break;
            case V1ActionPlan.Url url:
                action = new UrlAction(url.Target);
                break;
            case V1ActionPlan.App app:
                action = new AppAction(app.Target);
                break;
            case V1ActionPlan.SystemCommand system:
                action = new SystemAction(system.Command);
                fixedIcon = system.Icon;
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown planned action " + planned.Action.GetType().Name + "."
                );
        }

        var icon =
            context.SuggestIcon?.Invoke(planned.Name, suggestionKeys)
            ?? fixedIcon
            ?? ShortcutFallbackIcon;
        return new Shortcut(
            context.Ids.NewShortcutId(),
            LocalizedText.Same(planned.Name, LangCode.Es, LangCode.En),
            icon,
            AutoIcon: true,
            planned.Category,
            action,
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );
    }

    /// <summary>
    /// The counts of what was produced (MIG-004): profiles that came from v1, shortcuts plus the separators the report
    /// accounts for, web and app shortcuts.
    /// </summary>
    private static V1Counts Count(
        ShortcutLibrary library,
        V1Plan plan,
        ValueList<MigrationNote> notes
    )
    {
        var profiles = 0;
        var shortcuts = 0;
        var urls = 0;
        var apps = 0;
        foreach (var profile in library.Profiles)
        {
            if (!IsCreated(profile, plan))
            {
                profiles++;
            }

            foreach (var shortcut in profile.Shortcuts)
            {
                shortcuts++;
                urls += shortcut.Action is UrlAction ? 1 : 0;
                apps += shortcut.Action is AppAction ? 1 : 0;
            }
        }

        var separators = notes.Count(static n => n.Kind == MigrationNoteKind.Separator);
        return new V1Counts(profiles, shortcuts + separators, separators, urls, apps);
    }

    private static bool IsCreated(Profile profile, V1Plan plan) =>
        profile.Id == ProfileId.General
        && plan.Profiles.Any(static p => p.IsGeneral && p.IsCreated);
}
