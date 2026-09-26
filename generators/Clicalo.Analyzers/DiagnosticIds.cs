namespace Clicalo.Analyzers;

/// <summary>Stable identifiers of the Clícalo product rules. Never reuse or renumber an id.</summary>
public static class DiagnosticIds
{
    /// <summary>A non-activating window is shown or focused through an activating API.</summary>
    public const string NonActivatingWindow = "CLC0001";

    /// <summary>A sensitive value reaches a log sink or an exception.</summary>
    public const string SensitiveData = "CLC0003";

    /// <summary>A duration is written as a literal instead of a <c>Timings</c> constant.</summary>
    public const string DurationLiteral = "CLC0004";

    /// <summary>Visible text or a color is written as a literal instead of coming from data.</summary>
    public const string PresentationLiteral = "CLC0006";

    /// <summary>A destructive command is dispatched without a confirmation token, or a token is forged.</summary>
    public const string DestructiveCommand = "CLC0010";
}
