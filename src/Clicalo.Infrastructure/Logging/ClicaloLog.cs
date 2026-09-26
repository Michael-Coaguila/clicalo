using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// The product log (blueprint §9.4, LOG-001, ACE-003): Serilog to <c>%AppData%\Clicalo\logs\clicalo.log</c> through
/// <see cref="FixedNameRollingFileSink"/> (5 × 1 MB), Information by default and Debug on request, with redaction by
/// type (<see cref="Domain.Privacy.Sensitive{T}"/>, <see cref="Domain.Privacy.SecretText"/>) and without the Windows
/// user. The composition root bridges it to <c>Microsoft.Extensions.Logging</c>, where every message is a
/// <c>[LoggerMessage]</c> with a stable code.
/// </summary>
public static class ClicaloLog
{
    /// <summary>The line template: time, level, sequence, logical thread, event id and message.</summary>
    public const string Template =
        "{Timestamp:yyyy-MM-dd'T'HH:mm:ss.fffzzz} [{Level:u3}] #{Seq} {Thread} {EventCode} {Message:lj}{NewLine}{Exception}";

    /// <summary>Creates the logger of the product.</summary>
    /// <param name="locations">Where the data lives (<see cref="DataLocations.LogFile"/>).</param>
    /// <param name="level">The level switch (see <see cref="DebugLoggingWindow"/>).</param>
    public static Logger Create(DataLocations locations, LoggingLevelSwitch level) =>
        Create(locations, level, UserPathRedactor.ForCurrentUser());

    /// <summary>Creates the logger with another redactor (tests).</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="level">The level switch.</param>
    /// <param name="redactor">Removes the Windows user from the text.</param>
    public static Logger Create(
        DataLocations locations,
        LoggingLevelSwitch level,
        UserPathRedactor redactor
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(redactor);
        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(level)
            .Destructure.With(new SensitiveDestructuringPolicy())
            .Enrich.With(new LogEventEnricher(redactor))
            .WriteTo.Sink(
                new FixedNameRollingFileSink(
                    locations.LogFile,
                    Timings.Logging.LogFileMaxBytes,
                    Timings.Logging.LogFileCount,
                    new MessageTemplateTextFormatter(Template, CultureInfo.InvariantCulture),
                    redactor
                )
            )
            .CreateLogger();
    }

    /// <summary>The default level switch: Information.</summary>
    public static LoggingLevelSwitch DefaultLevel() => new(LogEventLevel.Information);
}
