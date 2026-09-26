using System.Text;
using SimpleExec;

namespace Clicalo.Build;

/// <summary>
/// Runs processes through SimpleExec from the current directory (the repository root). Exit codes are
/// returned, never thrown. SimpleExec's own echo is off: it prints one argument per line and the resolved
/// executable path, which Narrator would read aloud; callers echo one readable line when asked to.
/// </summary>
internal static class CommandRunner
{
    private static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false
    );

    /// <summary>Runs <paramref name="name"/> with its output going straight to the console.</summary>
    /// <param name="name">Executable on the PATH.</param>
    /// <param name="args">Arguments, unescaped.</param>
    /// <param name="environment">Variables to set, or to remove when the value is <see langword="null"/>.</param>
    public static async Task<int> RunAsync(
        string name,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?>? environment = null
    )
    {
        var exitCode = 0;
        await Command.RunAsync(
            name,
            args,
            configureEnvironment: variables => Apply(variables, environment),
            handleExitCode: code =>
            {
                exitCode = code;
                return true;
            },
            noEcho: true
        );
        return exitCode;
    }

    /// <summary>Runs <paramref name="name"/> and captures both streams.</summary>
    public static async Task<CommandOutput> ReadAsync(string name, IReadOnlyList<string> args)
    {
        var exitCode = 0;
        var (standardOutput, standardError) = await Command.ReadAsync(
            name,
            args,
            handleExitCode: code =>
            {
                exitCode = code;
                return true;
            },
            encoding: Utf8
        );
        return new CommandOutput(exitCode, standardOutput, standardError);
    }

    /// <summary>The command line as a person would type it again.</summary>
    public static string Display(string name, IEnumerable<string> args) =>
        string.Join(' ', args.Select(Quote).Prepend(name));

    private static string Quote(string argument) =>
        argument.Length > 0
        && !argument.Any(character => char.IsWhiteSpace(character) || character == '"')
            ? argument
            : "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void Apply(
        IDictionary<string, string?> variables,
        IReadOnlyDictionary<string, string?>? changes
    )
    {
        if (changes is null)
        {
            return;
        }

        foreach (var (key, value) in changes)
        {
            if (value is null)
            {
                variables.Remove(key);
            }
            else
            {
                variables[key] = value;
            }
        }
    }
}
