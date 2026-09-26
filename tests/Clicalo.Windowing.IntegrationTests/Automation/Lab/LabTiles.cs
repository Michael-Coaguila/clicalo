using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// The nine tiles of the S3 lab panel (docs/testing/spikes/S3.md): six Invoke tiles, the three-state Shift sticky
/// key, the Hold Ctrl latch and the profile button. Names are shortcut data, as a person would have typed them.
/// </summary>
public static class LabTiles
{
    /// <summary>The on state text of a Toggle tile (the view model's localized «ACTIVO»).</summary>
    public const string OnState = "ACTIVO";

    /// <summary>The locked state text of a three-state sticky key.</summary>
    public const string LockedState = "BLOQUEADA";

    /// <summary>The tiles, in panel order: tile N gets voice number N.</summary>
    public static IReadOnlyList<LabTileSpec> All { get; } =
    [
        new("tile.bold", "Negrita", ShortcutTilePattern.Invoke, "Ctrl + N"),
        new("tile.italic", "Cursiva", ShortcutTilePattern.Invoke, "Ctrl + K"),
        new("tile.underline", "Subrayado", ShortcutTilePattern.Invoke, "Ctrl + S"),
        new("tile.copy", "Copiar", ShortcutTilePattern.Invoke, "Ctrl + C"),
        new("tile.paste", "Pegar", ShortcutTilePattern.Invoke, "Ctrl + V"),
        new("tile.undo", "Deshacer", ShortcutTilePattern.Invoke, "Ctrl + Z"),
        new("tile.shift", "Mayús", ShortcutTilePattern.Toggle, "Mayús", ThreeStates: true),
        new("tile.holdCtrl", "Mantener Ctrl", ShortcutTilePattern.Toggle, "Ctrl"),
        new("tile.profile", "Perfil", ShortcutTilePattern.ExpandCollapse, string.Empty),
    ];

    /// <summary>The Invoke tiles.</summary>
    public static IEnumerable<LabTileSpec> Invokable =>
        All.Where(tile => tile.Pattern == ShortcutTilePattern.Invoke);

    /// <summary>The tile with <paramref name="id"/>.</summary>
    public static LabTileSpec Get(string id) =>
        All.Single(tile => string.Equals(tile.Id, id, StringComparison.Ordinal));

    /// <summary>Voice number of <paramref name="id"/> (its position, from 1).</summary>
    public static int VoiceNumberOf(string id) =>
        All.Select((tile, index) => (tile, index))
            .Single(entry => string.Equals(entry.tile.Id, id, StringComparison.Ordinal))
            .index + 1;
}
