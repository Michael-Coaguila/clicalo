using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Turns InputProbe lines into typed events. Also useful to replay a recorded trace.</summary>
public static class ProbeEventParser
{
    /// <summary>Parses one line. Throws <see cref="FormatException"/> when it breaks the protocol.</summary>
    public static ProbeEvent Parse(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        try
        {
            using var document = JsonDocument.Parse(line);
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("A probe event must be a JSON object.");
            }

            return JsonFields.String(json, ProbeFields.Kind) switch
            {
                ProbeEventKinds.Key => new KeyMessageEvent(json, line),
                ProbeEventKinds.Char => new CharMessageEvent(json, line),
                ProbeEventKinds.UniChar => new UniCharMessageEvent(json, line),
                ProbeEventKinds.RawKey => new RawKeyboardEvent(json, line),
                ProbeEventKinds.MouseButton => new MouseButtonEvent(json, line),
                ProbeEventKinds.Wheel => new MouseWheelEvent(json, line),
                ProbeEventKinds.Activate => new ActivateEvent(json, line),
                ProbeEventKinds.ActivateApp => new AppActivateEvent(json, line),
                ProbeEventKinds.Focus => new FocusEvent(json, line),
                ProbeEventKinds.InputLanguage => new InputLanguageEvent(json, line),
                ProbeEventKinds.Message => new WindowMessageEvent(json, line),
                ProbeEventKinds.Ready => new ProbeReadyEvent(json, line),
                ProbeEventKinds.Pong => new ProbePongEvent(json, line),
                ProbeEventKinds.Foreground => new ProbeForegroundEvent(json, line),
                ProbeEventKinds.Error => new ProbeErrorEvent(json, line),
                ProbeEventKinds.Exit => new ProbeExitEvent(json, line),
                _ => new UnknownProbeEvent(json, line),
            };
        }
        catch (JsonException ex)
        {
            throw new FormatException("Malformed probe event: " + line, ex);
        }
        catch (FormatException ex)
        {
            throw new FormatException(ex.Message + " Line: " + line, ex);
        }
    }
}
