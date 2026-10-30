namespace Clicalo.Application.UseCases.Templates;

/// <summary>Where the content of the preview of Plantillas comes from (PLA-015).</summary>
public enum PreviewSource
{
    /// <summary>A template that ships with Clícalo (CAT-006).</summary>
    Template,

    /// <summary>An AI proposal (PLA-002).</summary>
    Ai,

    /// <summary>A shared profile imported from a file (DAT-007, PLA-014).</summary>
    Shared,
}
