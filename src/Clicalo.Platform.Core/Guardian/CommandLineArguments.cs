using System.Text;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The command line of a process Clícalo starts itself through <see cref="GuardianProcess"/> (Sentinel and the main
/// process Sentinel relaunches, ADR-0032): each argument arrives as exactly one argument, as Windows splits a command
/// line. An argument never holds a quote or a control character, and no Windows path can hold one either, so the only
/// escape is the one a path needs: an argument with a space, or an empty one, goes between quotes with the backslashes
/// at its end doubled, so they do not escape the closing quote. Nothing else can be read as a quote, so a path never
/// adds an argument.
/// </summary>
/// <remarks>
/// Without quotes inside an argument, <c>CommandLineToArgvW</c>, the C runtime of <c>Clicalo.exe</c> and the start-up of
/// Native AOT split the same way: backslashes are literal unless a quote follows them, and then each pair is one.
/// </remarks>
public static class CommandLineArguments
{
    /// <summary>Whether <paramref name="argument"/> can be passed: it holds no quote and no control character.</summary>
    /// <param name="argument">The argument.</param>
    public static bool CanPass(string argument) =>
        argument is not null
        && !argument.Contains('"', StringComparison.Ordinal)
        && argument.AsSpan().IndexOfAnyInRange('\0', '\u001F') < 0;

    /// <summary>The arguments, each quoted when it needs it, separated by one space.</summary>
    /// <param name="arguments">The arguments, in order.</param>
    /// <exception cref="ArgumentException">An argument holds a quote or a control character.</exception>
    public static string Join(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var text = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            Append(text, argument);
        }

        return text.ToString();
    }

    /// <summary>Appends one argument, between quotes when it has a space or is empty.</summary>
    /// <param name="text">The command line so far.</param>
    /// <param name="argument">The argument.</param>
    /// <exception cref="ArgumentException">The argument holds a quote or a control character.</exception>
    private static void Append(StringBuilder text, string argument)
    {
        if (!CanPass(argument))
        {
            throw new ArgumentException(
                "An argument never holds a quote or a control character: " + argument,
                nameof(argument)
            );
        }

        if (argument.Length > 0 && !argument.Contains(' ', StringComparison.Ordinal))
        {
            text.Append(argument);
            return;
        }

        var trailingBackslashes = argument.Length - argument.AsSpan().TrimEnd('\\').Length;
        text.Append('"').Append(argument).Append('\\', trailingBackslashes).Append('"');
    }
}
