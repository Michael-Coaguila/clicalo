using System.Net;
using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// Gemini by Google with the person's own Google AI Studio key (ADR-0033, user decision D5): a fast, low-cost model
/// with a free tier, behind <see cref="GeminiChatClient"/>, no retries (the person retries from the error card) and the
/// time limit of <c>Timings.Ai.AiRequestTimeout</c>. The address is fixed here and the key goes only in a header, so no
/// environment variable can redirect the request or add a credential. On the free tier Google may use what is sent.
/// </summary>
internal sealed class GeminiProvider : IAiProvider
{
    /// <summary>The model: Gemini 3.5 Flash-Lite, stable, with a free tier, and minimal reasoning by default.</summary>
    public const string Model = "gemini-3.5-flash-lite";

    /// <summary>The only address a generation goes to.</summary>
    public static Uri Endpoint { get; } =
        new(
            "https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent"
        );

    private static readonly HttpClient SharedHttp = new() { Timeout = Timings.Ai.AiRequestTimeout };

    private readonly HttpClient? _http;

    /// <summary>Creates the provider.</summary>
    /// <param name="http">The HTTP client; null for the shared one. Tests pass one with a fake handler.</param>
    public GeminiProvider(HttpClient? http = null) => _http = http;

    /// <inheritdoc />
    public string Id => "gemini";

    /// <inheritdoc />
    public ChatOptions Options =>
        new() { ModelId = Model, MaxOutputTokens = Timings.Ai.AiMaxOutputTokens };

    /// <inheritdoc />
    public IChatClient Create(Sensitive<string> key) =>
        new GeminiChatClient(_http ?? SharedHttp, Endpoint, Model, key);

    /// <inheritdoc />
    public AiFailure Classify(Exception exception) =>
        exception switch
        {
            // R-26: «badkey» is a refused key or a limit of the provider, such as the daily free quota.
            GeminiApiException { KeyRejected: true } => AiFailure.BadKey,
            GeminiApiException
            {
                StatusCode: HttpStatusCode.Unauthorized
                    or HttpStatusCode.PaymentRequired
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.TooManyRequests,
            } => AiFailure.BadKey,
            GeminiApiException => AiFailure.Unavailable,
            HttpRequestException or TimeoutException => AiFailure.Offline,
            _ => AiFailure.Invalid,
        };
}
