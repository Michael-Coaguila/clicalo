using System.Collections.Immutable;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.SpikeLab.Input;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tiles;

/// <summary>
/// The tiles of every laboratory surface. The panel has the twelve tiles of S3.md plus «Localizar» (Ctrl+F) and
/// «Centro de control» (S4.md); its order fixes the voice numbers («clic 4» is «Copiar», «clic 7» is «Guardar»).
/// Names are unique across the surfaces shown together, so «clic Negrita» never needs disambiguation.
/// </summary>
internal static class LabTiles
{
    /// <summary>Id of «Negrita».</summary>
    public const string Bold = "bold";

    /// <summary>Id of «Copiar» (voice number 4).</summary>
    public const string Copy = "copy";

    /// <summary>Id of «Subrayado» (voice number 3).</summary>
    public const string Underline = "underline";

    /// <summary>Id of «Guardar» (voice number 7).</summary>
    public const string Save = "save";

    /// <summary>Id of «Mayús» (three-state toggle).</summary>
    public const string Shift = "shift";

    /// <summary>Id of «Mantener Ctrl» (toggle).</summary>
    public const string Control = "ctrl";

    /// <summary>Id of «Perfil» (expand and collapse).</summary>
    public const string Profile = "profile";

    /// <summary>Id of «Buscar» (opens the search).</summary>
    public const string Search = "search";

    /// <summary>Id of «Centro de control».</summary>
    public const string ControlCenter = "control-center";

    /// <summary>Id of «Soltar todo».</summary>
    public const string ReleaseAll = "release-all";

    /// <summary>The panel, in voice-number order (1 to 14).</summary>
    public static ImmutableArray<LabTile> Panel { get; } =
    [
        Send(Bold, "Negrita", "B", LabChord.Ctrl(VirtualKeyCode.B)),
        Send("italic", "Cursiva", "I", LabChord.Ctrl(VirtualKeyCode.I)),
        Send(Underline, "Subrayado", "U", LabChord.Ctrl(VirtualKeyCode.U)),
        Send(Copy, "Copiar", "⧉", LabChord.Ctrl(VirtualKeyCode.C)),
        Send("paste", "Pegar", "📋", LabChord.Ctrl(VirtualKeyCode.V)),
        Send("undo", "Deshacer", "↶", LabChord.Ctrl(VirtualKeyCode.Z)),
        Send(Save, "Guardar", "💾", LabChord.Ctrl(VirtualKeyCode.S)),
        Send("find", "Localizar", "⌕", LabChord.Ctrl(VirtualKeyCode.F)),
        new(Shift, "Mayús", "⇧", CommandPattern.Toggle, LabAction.CycleShift),
        new(Control, "Mantener Ctrl", "⌃", CommandPattern.Toggle, LabAction.ToggleControl),
        new(Profile, "Perfil", "👤", CommandPattern.ExpandCollapse, LabAction.ToggleProfile),
        new(Search, "Buscar", "🔍", CommandPattern.Invoke, LabAction.OpenSearch),
        new(
            ControlCenter,
            "Centro de control",
            "⚙",
            CommandPattern.Invoke,
            LabAction.OpenControlCenter
        ),
        new(ReleaseAll, "Soltar todo", "✋", CommandPattern.Invoke, LabAction.ReleaseAll),
    ];

    /// <summary>The edge bar: its handle (opens the side window) and three shortcuts.</summary>
    public static ImmutableArray<LabTile> Dock { get; } =
    [
        new(
            "dock-handle",
            "Asa de la Pestaña",
            "⋮",
            CommandPattern.ExpandCollapse,
            LabAction.ToggleSideWindow
        ),
        Send("dock-cut", "Cortar", "✂", LabChord.Ctrl(VirtualKeyCode.X)),
        Send("dock-select-all", "Seleccionar todo", "▣", LabChord.Ctrl(VirtualKeyCode.A)),
        Send("dock-redo", "Rehacer", "↷", LabChord.Ctrl(VirtualKeyCode.Y)),
    ];

    /// <summary>The side window of the edge bar.</summary>
    public static ImmutableArray<LabTile> Side { get; } =
    [
        Send("side-home", "Ir al inicio", "⤒", LabChord.Ctrl(LabChord.HomeKey)),
        Send("side-end", "Ir al final", "⤓", LabChord.Ctrl(LabChord.EndKey)),
    ];

    /// <summary>The bubble.</summary>
    public static ImmutableArray<LabTile> Bubble { get; } =
    [new("bubble", "Burbuja", "●", CommandPattern.Invoke, LabAction.BubbleTap)];

    /// <summary>The profile side window opened by «Perfil».</summary>
    public static ImmutableArray<LabTile> Profiles { get; } =
    [
        new("profile-general", "General", "", CommandPattern.Invoke, LabAction.ChooseProfile),
        new("profile-writing", "Escritura", "", CommandPattern.Invoke, LabAction.ChooseProfile),
    ];

    /// <summary>The search surface (an instrument of S4, not a surface under test).</summary>
    public static ImmutableArray<LabTile> SearchControls { get; } =
    [
        Instrument("search-dictate", "Dictar", "🎤", LabAction.SearchDictate),
        Instrument("search-result", "Resultado Negrita", "B", LabAction.SearchResult),
        Instrument("search-close", "Cerrar búsqueda", "✕", LabAction.SearchClose),
    ];

    /// <summary>The fixed buttons of the guide strip.</summary>
    public static ImmutableArray<LabTile> Guide { get; } =
    [
        Instrument("guide-worked", "Funcionó", "✓", LabAction.GuideWorked),
        Instrument("guide-failed", "Falló", "✗", LabAction.GuideFailed),
        Instrument("guide-repeat", "Repetir", "↺", LabAction.GuideRepeat),
        Instrument("guide-next", "Siguiente", "→", LabAction.GuideNext),
        Instrument("guide-release-all", "Soltar todo ya", "✋", LabAction.ReleaseAll),
    ];

    /// <summary>The step action button of the guide strip; its name follows the current step.</summary>
    public static LabTile GuideStepAction { get; } =
        Instrument("guide-step-action", "Acción del paso", "▶", LabAction.GuideStepAction);

    /// <summary>The 1-based voice number of a panel tile, or null when <paramref name="id"/> is not on the panel.</summary>
    public static int? VoiceNumberOf(string id)
    {
        for (var i = 0; i < Panel.Length; i++)
        {
            if (string.Equals(Panel[i].Id, id, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return null;
    }

    private static LabTile Send(string id, string name, string glyph, LabChord chord) =>
        new(id, name, glyph, CommandPattern.Invoke, LabAction.SendChord) { Chord = chord };

    private static LabTile Instrument(string id, string name, string glyph, LabAction action) =>
        new(id, name, glyph, CommandPattern.Invoke, action) { IsTestTarget = false };
}
