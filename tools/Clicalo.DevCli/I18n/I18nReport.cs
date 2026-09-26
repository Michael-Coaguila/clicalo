using System.Globalization;
using Clicalo.Generators.Localization;

namespace Clicalo.DevCli.I18n;

/// <summary>Prints i18n problems in the MSBuild canonical format, with paths relative to the repository root.</summary>
internal sealed class I18nReport(string root, TextWriter output)
{
    public int Errors { get; private set; }

    /// <summary>Last line for a failed run, readable aloud: «1 problem found…» or «3 problems found…».</summary>
    public string ErrorSummary =>
        Errors == 1
            ? "1 problem found. See the list above."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{Errors} problems found. See the list above."
            );

    public void Add(LocalizationIssue issue) =>
        Add(issue.Id, issue.Message, issue.Path, issue.Line, issue.Column);

    public void Add(string id, string message, string? path, int line, int column)
    {
        Errors++;
        if (path is null)
        {
            output.WriteLine("error " + id + ": " + message);
            return;
        }

        var location =
            line > 0
                ? string.Create(CultureInfo.InvariantCulture, $"({line},{column})")
                : string.Empty;
        output.WriteLine(
            RepositoryRoot.Relative(root, path) + location + ": error " + id + ": " + message
        );
    }
}
