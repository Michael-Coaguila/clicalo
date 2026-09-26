using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Shares one profile (DAT-007): <c>clicalo-perfil-&lt;id&gt;.json</c> with <c>"type":"profile-share"</c> and a schema
/// version. Texts are left out, and the user warned, unless the user chooses to include them in clear; DPAPI blobs are
/// never shared (they only open for this Windows user on this machine, ADR-0008). Reading is untrusted (LOG-006):
/// size, depth and count limits of <c>Timings.Import</c>, strict JSON, new ids for everything (DAT-004), nothing
/// executed, and the Web, App and Macro shortcuts counted for the one-by-one confirmation (LOG-008).
/// </summary>
public sealed class ProfileShareCodec
{
    private const string SharedId = "shared";

    private readonly TimeProvider _time;

    /// <summary>Creates the codec.</summary>
    /// <param name="time">Clock of <c>writtenAtUtc</c>.</param>
    public ProfileShareCodec(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>The suggested file name of a shared profile: <c>clicalo-perfil-&lt;id&gt;.json</c>.</summary>
    /// <param name="id">The profile.</param>
    public static string FileName(ProfileId id)
    {
        var safe = new string([
            .. (id.Value ?? string.Empty).Select(c =>
                char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-'
            ),
        ]);
        return "clicalo-perfil-" + (safe.Length == 0 ? SharedId : safe) + ".json";
    }

    /// <summary>Writes <paramref name="profile"/> to share.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="includeTextsInClear">The user chose to include the texts in clear.</param>
    public ProfileShareExport Export(Profile profile, bool includeTextsInClear)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var dto = LibraryMapper.EncodeProfile(
            profile,
            PreservedFields.None,
            includeTextsInClear ? TextExport.Plain : TextExport.Exclude
        );
        dto = dto with
        {
            Shortcuts = [.. (dto.Shortcuts ?? []).Select(s => s with { PinnedFrom = null })],
        };
        var root = new JsonObject
        {
            ["type"] = DocumentFormats.ProfileShare,
            ["schema"] = new JsonObject
            {
                ["major"] = DocumentFormats.ProfileShareSchema.Major,
                ["minor"] = DocumentFormats.ProfileShareSchema.Minor,
            },
            ["writtenBy"] = DocumentFormats.AppVersion,
            ["writtenAtUtc"] = _time
                .GetUtcNow()
                .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["profile"] = JsonSerializer.SerializeToNode(
                dto,
                DocumentJsonContext.Default.ProfileDto
            ),
        };
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, JsonText.Indented))
        {
            root.WriteTo(writer);
        }

        return new ProfileShareExport(
            FileName(profile.Id),
            buffer.WrittenMemory.ToArray(),
            includeTextsInClear ? 0 : Texts(profile).Count(t => t.IsAvailable && t.Length > 0)
        );
    }

    /// <summary>Reads a shared profile for the preview.</summary>
    /// <param name="utf8">The file bytes.</param>
    /// <param name="ids">New ids for the profile and its shortcuts.</param>
    public static Result<ProfileShareImport> Import(ReadOnlySpan<byte> utf8, IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (utf8.Length > Timings.Import.ShareMaxBytes)
        {
            return Fail(PersistenceFailures.ImportTooLargeCode);
        }

        var text = JsonText.WithoutBom(utf8);
        if (!Utf8.IsValid(text))
        {
            return Fail(PersistenceFailures.ImportUnreadableCode);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(
                text,
                nodeOptions: null,
                documentOptions: JsonText.Strict(Timings.Import.ShareMaxDepth)
            );
        }
        catch (JsonException)
        {
            return Fail(PersistenceFailures.ImportUnreadableCode);
        }

        if (
            root is not JsonObject share
            || share["type"] is not JsonValue type
            || !type.TryGetValue<string>(out var typeName)
            || !string.Equals(typeName, DocumentFormats.ProfileShare, StringComparison.Ordinal)
            || share["profile"] is not JsonObject profileNode
        )
        {
            return Fail(PersistenceFailures.ImportUnreadableCode);
        }

        if (
            share["schema"] is JsonObject schema
            && schema["major"] is JsonValue major
            && major.TryGetValue<int>(out var majorValue)
            && majorValue > DocumentFormats.ProfileShareSchema.Major
        )
        {
            return Fail(PersistenceFailures.SchemaNewerCode);
        }

        if ((profileNode["shortcuts"] as JsonArray)?.Count > Timings.Import.ShareMaxShortcuts)
        {
            return Fail(PersistenceFailures.ImportInvalidCode);
        }

        try
        {
            var dto =
                profileNode.Deserialize(DocumentJsonContext.Default.ProfileDto)
                ?? throw new InvalidDataException("profile");
            var (repaired, _) = DocumentRepair.Apply(
                new PayloadDto { Profiles = [dto with { Id = SharedId }] }
            );
            var keep = new LibraryMapper.PreservedCollector();
            var decoded = LibraryMapper.DecodeProfile(
                repaired.Profiles!.Single(p =>
                    string.Equals(p.Id, SharedId, StringComparison.Ordinal)
                ),
                keep
            );
            var profile = decoded with
            {
                Id = ids.NewProfileId(),
                Shortcuts = ValueListBuilder.From(
                    decoded.Shortcuts.Select(s =>
                        s with
                        {
                            Id = ids.NewShortcutId(),
                            PinnedFrom = null,
                        }
                    )
                ),
            };
            var risky = profile.Shortcuts.Count(s =>
                s.Action.Kind is ActionKind.Url or ActionKind.App or ActionKind.Macro
            );
            return Results.Ok(new ProfileShareImport(profile, keep.UnavailableTexts, risky));
        }
        catch (Exception ex) when (DocumentCodec.IsInvalidData(ex))
        {
            return Fail(PersistenceFailures.ImportInvalidCode);
        }
    }

    private static IEnumerable<SecretText> Texts(Profile profile)
    {
        foreach (var shortcut in profile.Shortcuts)
        {
            switch (shortcut.Action)
            {
                case TextAction text:
                    yield return text.Text;
                    break;
                case MacroAction macro:
                    foreach (var step in macro.Steps.OfType<TextStep>())
                    {
                        yield return step.Text;
                    }

                    break;
            }
        }
    }

    private static Result<ProfileShareImport> Fail(string code) =>
        Results.Fail<ProfileShareImport>(PersistenceFailures.Warning(code));
}
