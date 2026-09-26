namespace Clicalo.Build;

/// <summary>State shared by the steps of one <c>cl</c> run.</summary>
internal sealed class RunContext(TextWriter output, OutputMode mode)
{
    private readonly List<string> _notes = [];

    /// <summary>Where every line goes.</summary>
    public TextWriter Output => output;

    /// <summary>How much to write.</summary>
    public OutputMode Mode => mode;

    /// <summary>Runs and tracks the named steps.</summary>
    public StepRunner Steps { get; } = new(output, mode.GitHubActions);

    /// <summary>Short facts appended to a successful final line (test count, pending setup).</summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>Adds a fact to the final line.</summary>
    public void AddNote(string note) => _notes.Add(note);
}
