using System.Net;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// Gemini refused a request (ADR-0033): only its HTTP status and whether the key was the reason, which
/// <see cref="GeminiProvider"/> turns into an error card. The message is a stable code: never the body, never the key.
/// </summary>
internal sealed class GeminiApiException : Exception
{
    /// <summary>Creates the failure.</summary>
    /// <param name="status">The HTTP status of the answer.</param>
    /// <param name="keyRejected">Whether Gemini said the key is not valid, as it does with a 400.</param>
    public GeminiApiException(HttpStatusCode status, bool keyRejected)
        : base(
            "ai.gemini." + ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)
        )
    {
        StatusCode = status;
        KeyRejected = keyRejected;
    }

    /// <summary>Creates a failure without a status: the service is unavailable.</summary>
    public GeminiApiException()
        : this(HttpStatusCode.ServiceUnavailable, keyRejected: false) { }

    /// <summary>Creates a failure without a status: the service is unavailable.</summary>
    /// <param name="message">Ignored: the message is the stable code.</param>
    public GeminiApiException(string message)
        : this() => _ = message;

    /// <summary>Creates a failure without a status: the service is unavailable.</summary>
    /// <param name="message">Ignored: the message is the stable code.</param>
    /// <param name="inner">Ignored: nothing of the request travels with the failure.</param>
    public GeminiApiException(string message, Exception inner)
        : this() => _ = (message, inner);

    /// <summary>The HTTP status of the answer.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Whether Gemini said the key is not valid.</summary>
    public bool KeyRejected { get; }
}
