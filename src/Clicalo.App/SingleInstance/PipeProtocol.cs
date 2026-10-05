using System.IO;
using System.Text.Json;
using Clicalo.Domain.Timing;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// The wire format of the single-instance pipe (blueprint §3.4, ADR-0010), in its M2 form: one UTF-8 JSON message per
/// request and per response, at most <c>Timings.Ipc.IpcMaxMessageBytes</c>. The only verb is <c>show</c>:
/// <code>{"t":"show","v":1}</code> → <code>{"status":"ok"}</code>. <c>OpenUri</c> and <c>ImportFile</c> (a preview,
/// never applied) join with the IPC contract of <c>Platform.Core/Ipc</c>; no verb injects input, edits the document or
/// changes the foreground (D13). Parsing is strict and allocation-light: anything else is <see cref="PipeStatus.Rejected"/>
/// or <see cref="PipeStatus.Unsupported"/>, never an exception.
/// </summary>
internal static class PipeProtocol
{
    /// <summary>The protocol version of this build.</summary>
    public const int Version = 1;

    /// <summary>The only verb of M2.</summary>
    public const string ShowVerb = "show";

    private const string VerbProperty = "t";
    private const string VersionProperty = "v";
    private const string StatusProperty = "status";

    /// <summary>The request that asks the running instance to show the panel.</summary>
    public static byte[] ShowRequest() => Write(writer => WriteRequest(writer, ShowVerb, Version));

    /// <summary>A request with any verb and version (tests of the server's refusals).</summary>
    public static byte[] Request(string verb, int version) =>
        Write(writer => WriteRequest(writer, verb, version));

    /// <summary>The response with <paramref name="status"/>.</summary>
    public static byte[] Response(PipeStatus status) =>
        Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString(StatusProperty, Name(status));
            writer.WriteEndObject();
        });

    /// <summary>Reads a request: <see cref="PipeStatus.Ok"/> only for a well-formed <c>show</c> of this version.</summary>
    /// <param name="message">The bytes received.</param>
    public static PipeStatus ReadRequest(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty || message.Length > Timings.Ipc.IpcMaxMessageBytes)
        {
            return PipeStatus.Rejected;
        }

        string? verb = null;
        int? version = null;
        try
        {
            var reader = new Utf8JsonReader(
                message,
                new JsonReaderOptions
                {
                    MaxDepth = 2,
                    CommentHandling = JsonCommentHandling.Disallow,
                }
            );
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return PipeStatus.Rejected;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(VerbProperty) && verb is null)
                {
                    verb =
                        reader.Read() && reader.TokenType == JsonTokenType.String
                            ? reader.GetString()
                            : null;
                    if (verb is null)
                    {
                        return PipeStatus.Rejected;
                    }
                }
                else if (reader.ValueTextEquals(VersionProperty) && version is null)
                {
                    // TryGetInt32 throws on anything but a number, so the type is checked first.
                    if (
                        !reader.Read()
                        || reader.TokenType != JsonTokenType.Number
                        || !reader.TryGetInt32(out var number)
                    )
                    {
                        return PipeStatus.Rejected;
                    }

                    version = number;
                }
                else
                {
                    return PipeStatus.Rejected;
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                return PipeStatus.Rejected;
            }
        }
        catch (JsonException)
        {
            return PipeStatus.Rejected;
        }

        if (verb is null || version is null)
        {
            return PipeStatus.Rejected;
        }

        return string.Equals(verb, ShowVerb, StringComparison.Ordinal) && version == Version
            ? PipeStatus.Ok
            : PipeStatus.Unsupported;
    }

    /// <summary>Reads a response; <see langword="null"/> when it is malformed.</summary>
    /// <param name="message">The bytes received.</param>
    public static PipeStatus? ReadResponse(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty || message.Length > Timings.Ipc.IpcMaxMessageBytes)
        {
            return null;
        }

        try
        {
            var reader = new Utf8JsonReader(message, new JsonReaderOptions { MaxDepth = 2 });
            if (
                !reader.Read()
                || reader.TokenType != JsonTokenType.StartObject
                || !reader.Read()
                || reader.TokenType != JsonTokenType.PropertyName
                || !reader.ValueTextEquals(StatusProperty)
                || !reader.Read()
                || reader.TokenType != JsonTokenType.String
            )
            {
                return null;
            }

            var status = reader.GetString();
            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                return null;
            }

            foreach (var candidate in Enum.GetValues<PipeStatus>())
            {
                if (string.Equals(status, Name(candidate), StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Name(PipeStatus status) =>
        status switch
        {
            PipeStatus.Ok => "ok",
            PipeStatus.Rejected => "rejected",
            PipeStatus.Unsupported => "unsupported",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static void WriteRequest(Utf8JsonWriter writer, string verb, int version)
    {
        writer.WriteStartObject();
        writer.WriteString(VerbProperty, verb);
        writer.WriteNumber(VersionProperty, version);
        writer.WriteEndObject();
    }

    private static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return buffer.ToArray();
    }
}
