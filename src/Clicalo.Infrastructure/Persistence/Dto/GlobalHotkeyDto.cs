namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The global shortcut that shows or hides the panel (BUR-005, schema 1.1, ADR-0028).</summary>
internal sealed record GlobalHotkeyDto
{
    public bool? Enabled { get; init; }

    /// <summary>An id of <c>data/catalogs/global-hotkeys.json</c>.</summary>
    public string? Combo { get; init; }
}
