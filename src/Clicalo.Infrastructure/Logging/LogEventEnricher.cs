using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// Adds what the log template shows (blueprint §9.4): a process-wide sequence number <c>Seq</c>, the logical thread
/// <c>Thread</c> (its name: UI, Engine, SysEvents, Shell, Persistence…, or its id) and <c>EventCode</c>, the numeric id of
/// the <c>[LoggerMessage]</c>. It also cleans every captured value, nested ones included (LOG-001): the Windows user
/// leaves every text, and an address keeps only its scheme and host.
/// </summary>
internal sealed class LogEventEnricher : ILogEventEnricher
{
    private readonly UserPathRedactor _redactor;
    private long _seq;

    /// <summary>Creates the enricher.</summary>
    /// <param name="redactor">Removes the user from text.</param>
    public LogEventEnricher(UserPathRedactor redactor) => _redactor = redactor;

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);
        foreach (var property in logEvent.Properties.ToArray())
        {
            var clean = Clean(property.Value);
            if (!ReferenceEquals(clean, property.Value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, clean));
            }
        }

        logEvent.AddPropertyIfAbsent(
            new LogEventProperty("Seq", new ScalarValue(Interlocked.Increment(ref _seq)))
        );
        var thread =
            Thread.CurrentThread.Name
            ?? "#" + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        logEvent.AddPropertyIfAbsent(new LogEventProperty("Thread", new ScalarValue(thread)));
        logEvent.AddPropertyIfAbsent(
            new LogEventProperty("EventCode", new ScalarValue(EventCode(logEvent)))
        );
    }

    private static object EventCode(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("EventId", out var eventId)
        && eventId is StructureValue structure
        && structure.Properties.FirstOrDefault(p =>
            string.Equals(p.Name, "Id", StringComparison.Ordinal)
        )
            is { Value: ScalarValue { Value: { } id } }
            ? id
            : "-";

    /// <summary>The value without the user and without full addresses; the same instance when nothing changed.</summary>
    private LogEventPropertyValue Clean(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                var redacted = _redactor.Redact(text);
                return string.Equals(redacted, text, StringComparison.Ordinal)
                    ? value
                    : new ScalarValue(redacted);
            case ScalarValue { Value: Uri uri }:
                return new ScalarValue(
                    uri.IsAbsoluteUri ? uri.Scheme + "://" + uri.Host + "/…" : "[url]"
                );
            case SequenceValue sequence:
            {
                var changed = false;
                var elements = new List<LogEventPropertyValue>(sequence.Elements.Count);
                foreach (var element in sequence.Elements)
                {
                    var clean = Clean(element);
                    changed |= !ReferenceEquals(clean, element);
                    elements.Add(clean);
                }

                return changed ? new SequenceValue(elements) : value;
            }

            case StructureValue structure:
            {
                var changed = false;
                var properties = new List<LogEventProperty>(structure.Properties.Count);
                foreach (var property in structure.Properties)
                {
                    var clean = Clean(property.Value);
                    changed |= !ReferenceEquals(clean, property.Value);
                    properties.Add(new LogEventProperty(property.Name, clean));
                }

                return changed ? new StructureValue(properties, structure.TypeTag) : value;
            }

            case DictionaryValue dictionary:
            {
                var changed = false;
                var entries = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(
                    dictionary.Elements.Count
                );
                foreach (var pair in dictionary.Elements)
                {
                    var clean = Clean(pair.Value);
                    changed |= !ReferenceEquals(clean, pair.Value);
                    entries.Add(new(pair.Key, clean));
                }

                return changed ? new DictionaryValue(entries) : value;
            }

            default:
                return value;
        }
    }
}
