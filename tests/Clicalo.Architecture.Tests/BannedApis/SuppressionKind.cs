namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>How an RS0030 suppression is written.</summary>
public enum SuppressionKind
{
    /// <summary>A justified [SuppressMessage], or a justified pragma restored in the same file.</summary>
    Scoped,

    /// <summary>No justification.</summary>
    Unjustified,

    /// <summary>A pragma that is never restored, so it covers the rest of the file.</summary>
    Unrestored,

    /// <summary>An assembly-level or module-level suppression.</summary>
    Global,

    /// <summary>A pragma without diagnostic ids, which disables every warning, RS0030 included.</summary>
    Blanket,
}
