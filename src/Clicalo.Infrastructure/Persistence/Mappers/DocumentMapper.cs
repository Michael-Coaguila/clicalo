using System.Collections.Immutable;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence.Dto;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// The whole payload ↔ <see cref="UserDocument"/> (blueprint §6.5): the DTOs are repaired first
/// (<see cref="DocumentRepair"/>), the library is built with <c>ShortcutLibrary.CreateValidated</c>, the settings are
/// clamped by <c>SettingsSchema.Clamp</c> and the result must pass <c>UserDocument.Validate</c>.
/// </summary>
internal static class DocumentMapper
{
    /// <summary>A canonical combination in <c>dupIgnored</c> that could not be read was dropped.</summary>
    public const string DupIgnoredDropped = "repair.dup_ignored";

    /// <summary>A setting out of range was clamped.</summary>
    public const string SettingsClamped = "repair.settings_range";

    /// <summary>Reads a repaired payload; throws <see cref="InvalidDataException"/> when it is not a valid document.</summary>
    /// <param name="payload">The payload after <see cref="DocumentRepair.Apply"/>.</param>
    /// <param name="repairs">The repairs already made.</param>
    public static DecodedPayload Decode(PayloadDto payload, ImmutableArray<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var keep = new LibraryMapper.PreservedCollector();
        var profiles = ValueListBuilder.From(
            (payload.Profiles ?? []).Select(p => LibraryMapper.DecodeProfile(p, keep))
        );
        var always = LibraryMapper.DecodeShortcuts(payload.Always?.Shortcuts, keep);
        var library = ShortcutLibrary.CreateValidated(always, profiles);
        if (!library.TryGetValue(out var validLibrary))
        {
            throw new InvalidDataException("library: " + library.Failure.Code);
        }

        var found = new SortedSet<string>(repairs, StringComparer.Ordinal);
        var settings = SettingsSchema.Clamp(
            SettingsMapper.Decode(payload.Settings, SettingsSchema.Defaults),
            out var clamped
        );
        if (!clamped.IsDefaultOrEmpty)
        {
            _ = found.Add(SettingsClamped);
        }

        var ignored = new List<CanonicalChord>();
        foreach (var text in payload.DupIgnored ?? [])
        {
            if (CanonicalChord.TryParse(text, out var chord))
            {
                ignored.Add(chord);
            }
            else
            {
                _ = found.Add(DupIgnoredDropped);
            }
        }

        var frequents = new FrequentsState(
            Ids(payload.Frequents?.Pins),
            Ids(payload.Frequents?.Hidden),
            payload.Frequents?.UsageEpoch ?? 0,
            UsageMapper.Decode(payload.Usage)
        );
        var document = new UserDocument(
            0,
            validLibrary,
            frequents,
            new DuplicatePolicy(ValueListBuilder.From(ignored)),
            settings,
            new OnboardingState(payload.Onboarding?.Completed ?? false)
        );
        var violations = document.Validate();
        if (!violations.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("document: " + violations[0].Invariant);
        }

        return new DecodedPayload(document, keep.Build(payload), [.. found], keep.UnavailableTexts);
    }

    /// <summary>The persisted form of <paramref name="document"/>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="keep">What a rewrite must keep.</param>
    /// <param name="includeUsage">Whether the usage goes in the payload (backups, §6.8).</param>
    public static PayloadDto Encode(UserDocument document, PreservedFields keep, bool includeUsage)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(keep);
        return new PayloadDto
        {
            Settings = SettingsMapper.Encode(document.Settings, keep.Settings),
            Always = new ShortcutListDto
            {
                Shortcuts = LibraryMapper.EncodeShortcuts(
                    document.Library.AlwaysVisible,
                    keep,
                    TextExport.Encrypt
                ),
                Extra = LibraryMapper.Copy(keep.Always),
            },
            Profiles =
            [
                .. document.Library.Profiles.Select(p =>
                    LibraryMapper.EncodeProfile(p, keep, TextExport.Encrypt)
                ),
            ],
            Frequents = new FrequentsDto
            {
                Pins = [.. document.Frequents.Pins.Select(id => id.Value)],
                Hidden = [.. document.Frequents.Hidden.Select(id => id.Value)],
                UsageEpoch = document.Frequents.UsageEpoch,
                Extra = LibraryMapper.Copy(keep.Frequents),
            },
            DupIgnored = [.. document.Duplicates.Ignored.Select(c => c.ToStableString())],
            Onboarding = new OnboardingDto
            {
                Completed = document.Onboarding.Completed,
                Extra = LibraryMapper.Copy(keep.Onboarding),
            },
            Usage = includeUsage ? UsageMapper.Encode(document.Frequents.Usage) : null,
            Extra = LibraryMapper.Copy(keep.Root),
        };
    }

    private static ValueList<ShortcutId> Ids(List<string>? ids) =>
        ValueListBuilder.From(
            (ids ?? []).Where(id => id.Length > 0).Select(id => new ShortcutId(id))
        );
}
