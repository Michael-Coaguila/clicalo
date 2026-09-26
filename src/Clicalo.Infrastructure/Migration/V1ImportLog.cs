using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// The log lines of the v1 import: codes, kinds and counts only. Never a label, a combination, an address, a command
/// or a path, which may carry the user name (LOG-001).
/// </summary>
internal static partial class V1ImportLog
{
    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Information,
        Message = "v1 import converted a {Source}: {Profiles} profiles and {Buttons} buttons, {Notes} report lines"
    )]
    public static partial void Converted(
        ILogger logger,
        V1SourceKind source,
        int profiles,
        int buttons,
        int notes
    );

    [LoggerMessage(EventId = 402, Level = LogLevel.Warning, Message = "v1 import failed: {Code}")]
    public static partial void Failed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 403,
        Level = LogLevel.Information,
        Message = "v1 original kept before converting ({Bytes} bytes)"
    )]
    public static partial void OriginalKept(ILogger logger, int bytes);
}
