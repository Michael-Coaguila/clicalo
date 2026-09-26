using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab;

/// <summary>
/// The command line of SpikeLab: <c>dotnet run --project tools\SpikeLab -- --spike S1</c> (or S3, S4). Without
/// <c>--spike</c> the control window asks which spike to run.
/// </summary>
/// <param name="Spike">The spike to run, or null to choose in the control window.</param>
/// <param name="ReportDirectory">Where the reports go; null for <c>%LOCALAPPDATA%\Clicalo.SpikeLab\reports</c>.</param>
/// <param name="CheckOnly">
/// <c>--check</c>: compose every real piece without showing anything, write the list of pieces and their state to the
/// reports folder and exit (0 when every piece is ready, 2 otherwise).
/// </param>
internal sealed record LabOptions(SpikeId? Spike, string? ReportDirectory, bool CheckOnly)
{
    /// <summary>Exit code of <c>--check</c> when some piece is not ready.</summary>
    public const int PiecesNotReadyExitCode = 2;

    /// <summary>The options when the command line is empty.</summary>
    public static LabOptions Default { get; } = new(null, null, false);

    /// <summary>Parses <paramref name="args"/>; <paramref name="error"/> explains the first problem found.</summary>
    public static LabOptions Parse(IReadOnlyList<string> args, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        error = null;
        var options = Default;
        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = Split(args[i]);
            switch (name)
            {
                case "--spike":
                case "-s":
                    var spike = inlineValue ?? Next(args, ref i);
                    if (TryParseSpike(spike, out var parsed))
                    {
                        options = options with { Spike = parsed };
                    }
                    else
                    {
                        error ??= "Spike desconocido: «" + spike + "». Usa S1, S3 o S4.";
                    }

                    break;
                case "--reports":
                    var directory = inlineValue ?? Next(args, ref i);
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        error ??= "Falta la carpeta después de --reports.";
                    }
                    else
                    {
                        options = options with { ReportDirectory = directory };
                    }

                    break;
                case "--check":
                    options = options with { CheckOnly = true };
                    break;
                default:
                    error ??= "Argumento desconocido: «" + args[i] + "».";
                    break;
            }
        }

        return options;
    }

    /// <summary>Accepts S1, s1, S3 and S4.</summary>
    public static bool TryParseSpike(string? text, out SpikeId spike)
    {
        spike = default;
        return !string.IsNullOrWhiteSpace(text)
            && text.Trim().Length == 2
            && Enum.TryParse(text.Trim(), ignoreCase: true, out spike)
            && Enum.IsDefined(spike);
    }

    private static (string Name, string? Value) Split(string argument)
    {
        var equals = argument.IndexOf('=', StringComparison.Ordinal);
        return equals > 0
            ? (argument[..equals].ToLowerInvariant(), argument[(equals + 1)..])
            : (argument.ToLowerInvariant(), null);
    }

    private static string? Next(IReadOnlyList<string> args, ref int index) =>
        index + 1 < args.Count ? args[++index] : null;
}
