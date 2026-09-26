using System.Globalization;

namespace Clicalo.Build;

/// <summary>
/// Formats elapsed time the way the final line is read aloud by Narrator: whole units, no decimals and no
/// colons ("1 min 12 s" rather than "00:01:12.345").
/// </summary>
internal static class SpokenDuration
{
    /// <summary>Formats <paramref name="elapsed"/>; negative values are treated as zero.</summary>
    public static string Format(TimeSpan elapsed)
    {
        var totalSeconds = (long)Math.Floor(Math.Max(0, elapsed.TotalSeconds));
        if (totalSeconds < 1)
        {
            return Messages.LessThanOneSecond;
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return minutes > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{hours} h {minutes} min")
                : string.Create(CultureInfo.InvariantCulture, $"{hours} h");
        }

        if (minutes > 0)
        {
            return seconds > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{minutes} min {seconds} s")
                : string.Create(CultureInfo.InvariantCulture, $"{minutes} min");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{seconds} s");
    }
}
