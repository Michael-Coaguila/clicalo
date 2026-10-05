using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The crash journal (<c>%LocalAppData%\Clicalo\crash-journal.json</c>, blueprint §6.5): the times of the recent
/// crashes of the main process, which <see cref="RelaunchPolicy"/> counts (<c>Timings.App.CrashLoop</c>). Pure format
/// code, shared by Sentinel (which reads it) and the main process (which appends the crash it was relaunched after).
/// AOT-safe: <see cref="Utf8JsonReader"/> and <see cref="Utf8JsonWriter"/> only.
/// </summary>
/// <remarks>
/// <code>{ "format": "clicalo.crash-journal", "version": 1, "crashes": [ "2026-09-26T10:00:00.0000000+00:00", … ] }</code>
/// A journal that cannot be read counts as empty: a damaged journal can delay safe mode, never prevent a release.
/// </remarks>
public static class CrashJournal
{
    /// <summary>The value of <c>format</c>.</summary>
    public const string Format = "clicalo.crash-journal";

    /// <summary>The file name, under <c>%LocalAppData%\Clicalo</c>.</summary>
    public const string FileName = "crash-journal.json";

    /// <summary>The relaunch argument with the time of the crash, in Unix milliseconds (UTC).</summary>
    public const string AfterCrashArgument = "--after-crash=";

    /// <summary>The relaunch argument that asks for safe mode (the crash loop was reached).</summary>
    public const string SafeModeArgument = "--safe-mode";

    /// <summary>The journal kept small: older crashes than this many never matter.</summary>
    public const int MaxEntries = 32;

    /// <summary>The crash times of a journal; empty when it is missing or unreadable.</summary>
    /// <param name="json">The file's bytes.</param>
    public static ImmutableArray<DateTimeOffset> Parse(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(
                json,
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip }
            );
            var crashes = ImmutableArray.CreateBuilder<DateTimeOffset>();
            var formatOk = false;
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return [];
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                reader.Read();
                switch (name)
                {
                    case "format":
                        formatOk =
                            reader.TokenType == JsonTokenType.String
                            && string.Equals(reader.GetString(), Format, StringComparison.Ordinal);
                        break;
                    case "crashes" when reader.TokenType == JsonTokenType.StartArray:
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (
                                reader.TokenType == JsonTokenType.String
                                && DateTimeOffset.TryParse(
                                    reader.GetString(),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind,
                                    out var crash
                                )
                            )
                            {
                                crashes.Add(crash);
                            }
                        }

                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return formatOk ? crashes.ToImmutable() : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The journal with <paramref name="crash"/> appended, keeping the <see cref="MaxEntries"/> newest.</summary>
    /// <param name="previous">The crashes already recorded.</param>
    /// <param name="crash">The new crash.</param>
    public static byte[] Append(IEnumerable<DateTimeOffset> previous, DateTimeOffset crash)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("version", 1);
            writer.WriteStartArray("crashes");
            foreach (var entry in previous.Append(crash).Order().TakeLast(MaxEntries))
            {
                writer.WriteStringValue(entry.ToString("O", CultureInfo.InvariantCulture));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>The arguments Sentinel relaunches the main process with.</summary>
    /// <param name="crash">When the main process died.</param>
    /// <param name="safeMode">Whether the crash loop was reached.</param>
    public static ImmutableArray<string> RelaunchArguments(DateTimeOffset crash, bool safeMode)
    {
        var after =
            AfterCrashArgument
            + crash.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return safeMode ? [after, SafeModeArgument] : [after];
    }
}
