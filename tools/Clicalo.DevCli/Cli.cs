using Clicalo.DevCli.I18n;

namespace Clicalo.DevCli;

/// <summary>Verb dispatch and option parsing. Output ends with one line that Narrator can read aloud.</summary>
internal static class Cli
{
    private const string CheckVerb = "i18n-check";
    private const string ImportVerb = "i18n-import";

    private const string Help = """
        Clicalo developer CLI (behind `cl`).

        Usage: Clicalo.DevCli <verb> [options]

        Verbs:
          i18n-check    Validate data/i18n exactly like the build (CLCI errors), check the CLDR plural
                        rules and check data/i18n/allow-unused.txt against the code.
                        --strict-unused  also fail on keys that no code uses and allow-unused.txt does not list.
          i18n-import   Rebuild data/i18n/strings.*.json from the design handoff and data/i18n/handoff-import.json.
                        --check          do not write; fail when data/i18n differs from a fresh import.
          help          Show this help.

        Common options:
          --repo <dir>  Repository root (default: the closest folder with Clicalo.slnx above the current one).

        Exit codes: 0 no problems, 1 problems found, 2 usage error.
        """;

    public static int Run(
        IReadOnlyList<string> args,
        string workingDirectory,
        TextWriter output,
        TextWriter error
    )
    {
        if (args.Count == 0)
        {
            error.WriteLine(Help);
            error.WriteLine("No verb given.");
            return ExitCodes.Usage;
        }

        var verb = args[0];
        if (verb is "help" or "--help" or "-h")
        {
            output.WriteLine(Help);
            return ExitCodes.Success;
        }

        if (verb is not (CheckVerb or ImportVerb))
        {
            error.WriteLine(Help);
            error.WriteLine("Unknown verb '" + verb + "'.");
            return ExitCodes.Usage;
        }

        if (!TryParseOptions(verb, args.Skip(1).ToList(), out var options, out var problem))
        {
            error.WriteLine(Help);
            error.WriteLine(problem);
            return ExitCodes.Usage;
        }

        var root = options.Repo is null
            ? RepositoryRoot.Find(workingDirectory)
            : Path.GetFullPath(options.Repo);
        if (root is null || !File.Exists(Path.Combine(root, "Clicalo.slnx")))
        {
            error.WriteLine(
                "The repository root (the folder with Clicalo.slnx) was not found; pass --repo <dir>."
            );
            return ExitCodes.Usage;
        }

        return string.Equals(verb, CheckVerb, StringComparison.Ordinal)
            ? I18nCheckCommand.Run(root, options.StrictUnused, output)
            : I18nImportCommand.Run(root, options.Check, output);
    }

    private static bool TryParseOptions(
        string verb,
        List<string> args,
        out CliOptions options,
        out string problem
    )
    {
        options = new CliOptions(null, false, false);
        problem = string.Empty;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--repo" when i + 1 < args.Count:
                    options = options with { Repo = args[++i] };
                    break;
                case "--check" when string.Equals(verb, ImportVerb, StringComparison.Ordinal):
                    options = options with { Check = true };
                    break;
                case "--strict-unused"
                    when string.Equals(verb, CheckVerb, StringComparison.Ordinal):
                    options = options with { StrictUnused = true };
                    break;
                default:
                    problem = "Unknown or incomplete option '" + args[i] + "' for " + verb + ".";
                    return false;
            }
        }

        return true;
    }
}
