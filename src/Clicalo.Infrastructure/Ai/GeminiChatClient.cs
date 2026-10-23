using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// A minimal <see cref="IChatClient"/> over the native Gemini API <c>generateContent</c> (ADR-0033): one request with the
/// system instruction, the conversation and the output ceiling, and nothing else; the key goes in the
/// <c>x-goog-api-key</c> header, never in the address. No retries and no streaming. A refused request throws
/// <see cref="GeminiApiException"/>; an answer without text, blocked or larger than <c>Timings.Ai.AiResponseMaxBytes</c>
/// comes back empty, so the generator shows «invalid». Nothing of the request or the answer is logged.
/// </summary>
internal sealed class GeminiChatClient : IChatClient
{
    private const string KeyHeader = "x-goog-api-key";
    private const int MaxBody = (int)Timings.Ai.AiResponseMaxBytes;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly Sensitive<string> _key;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The HTTP client; it is shared, so this client never disposes it.</param>
    /// <param name="endpoint">The <c>generateContent</c> address of the model.</param>
    /// <param name="model">The model in the address.</param>
    /// <param name="key">The person's own key.</param>
    public GeminiChatClient(HttpClient http, Uri endpoint, string model, Sensitive<string> key)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(endpoint);
        _http = http;
        _endpoint = endpoint;
        _model = model;
        _key = key;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(messages);

        // A Google key is printable ASCII: anything else is not sent and reads as a refused key.
        var key = _key.Value;
        if (string.IsNullOrEmpty(key) || key.Any(static c => c is < '!' or > '~'))
        {
            throw new GeminiApiException(HttpStatusCode.Unauthorized, keyRejected: true);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(Body(messages, options)),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };
        request.Headers.Add(KeyHeader, key);
        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await ReadAsync(response.Content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new GeminiApiException(
                response.StatusCode,
                body is not null && body.AsSpan().IndexOf("API_KEY_INVALID"u8) >= 0
            );
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, Text(body)))
        {
            ModelId = _model,
        };
    }

    /// <inheritdoc />
    /// <remarks>A template is one short answer: the generator never streams.</remarks>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The HTTP client is shared between generations (ADR-0014): nothing to release here.
    }

    /// <summary>
    /// The request: the system messages as <c>systemInstruction</c>, the others as <c>contents</c>, and the output
    /// ceiling with a JSON answer. No safety settings, tools, cache, labels or identifiers (PLA-008).
    /// </summary>
    private static byte[] Body(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            var system = string.Concat(
                messages.Where(static m => m.Role == ChatRole.System).Select(static m => m.Text)
            );
            if (system.Length > 0)
            {
                writer.WriteStartObject("systemInstruction");
                WriteParts(writer, system);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("contents");
            foreach (var message in messages.Where(static m => m.Role != ChatRole.System))
            {
                writer.WriteStartObject();
                writer.WriteString("role", message.Role == ChatRole.Assistant ? "model" : "user");
                WriteParts(writer, message.Text);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartObject("generationConfig");
            if (options?.MaxOutputTokens is { } ceiling)
            {
                writer.WriteNumber("maxOutputTokens", ceiling);
            }

            writer.WriteString("responseMimeType", "application/json");
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteParts(Utf8JsonWriter writer, string text)
    {
        writer.WriteStartArray("parts");
        writer.WriteStartObject();
        writer.WriteString("text", text);
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    /// <summary>The body, or null when it is larger than an AI answer may be (LOG-006).</summary>
    private static async Task<byte[]?> ReadAsync(
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        if (content.Headers.ContentLength > MaxBody)
        {
            return null;
        }

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBody + 1];
            var total = 0;
            int read;
            while (
                total < buffer.Length
                && (
                    read = await stream
                        .ReadAsync(buffer.AsMemory(total), cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                total += read;
            }

            return total > MaxBody ? null : buffer[..total];
        }
    }

    /// <summary>
    /// The answer of the first candidate without its reasoning parts; empty when there is none (a blocked prompt, no
    /// candidates, a body over the limit or not JSON).
    /// </summary>
    private static string Text(byte[]? body)
    {
        if (body is null)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (
                !document.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Object
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array
            )
            {
                return string.Empty;
            }

            var text = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (
                    part.ValueKind == JsonValueKind.Object
                    && !(
                        part.TryGetProperty("thought", out var thought)
                        && thought.ValueKind == JsonValueKind.True
                    )
                    && part.TryGetProperty("text", out var value)
                    && value.ValueKind == JsonValueKind.String
                )
                {
                    _ = text.Append(value.GetString());
                }
            }

            return text.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
