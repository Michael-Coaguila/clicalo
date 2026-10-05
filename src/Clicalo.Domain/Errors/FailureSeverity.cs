namespace Clicalo.Domain.Errors;

/// <summary>How serious an <see cref="Failure"/> is.</summary>
public enum FailureSeverity
{
    /// <summary>Nothing was lost; the user may want to know.</summary>
    Info,

    /// <summary>The operation did not happen; nothing was lost.</summary>
    Warning,

    /// <summary>Something the user relies on is not working (unsaved changes, for example).</summary>
    Critical,
}
