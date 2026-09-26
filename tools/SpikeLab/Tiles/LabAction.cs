namespace Clicalo.Tools.SpikeLab.Tiles;

/// <summary>What a tile of the laboratory does when it is tapped or invoked by UI Automation.</summary>
internal enum LabAction
{
    /// <summary>Sends its chord to the app in front (only while «Enviar teclas» is on), with the latched modifiers.</summary>
    SendChord,

    /// <summary>«Mayús»: Off → once → locked → Off (the three states of a sticky key).</summary>
    CycleShift,

    /// <summary>«Mantener Ctrl»: latches Ctrl for the next chords until tapped again.</summary>
    ToggleControl,

    /// <summary>«Soltar todo»: clears the latches and releases any left modifier that is down.</summary>
    ReleaseAll,

    /// <summary>«Buscar»: opens the search surface under a <c>TextInput</c> lease.</summary>
    OpenSearch,

    /// <summary>«Centro de control»: opens the lab Control Center under a <c>ControlCenter</c> lease.</summary>
    OpenControlCenter,

    /// <summary>«Perfil»: expands or collapses the profile side window.</summary>
    ToggleProfile,

    /// <summary>The handle of the edge bar: opens or closes its side window.</summary>
    ToggleSideWindow,

    /// <summary>The bubble: only flashes (it is a target of S1).</summary>
    BubbleTap,

    /// <summary>A profile of the profile side window: flashes and closes it.</summary>
    ChooseProfile,

    /// <summary>The search result: gives the foreground back (M1 sends no action, S4.md).</summary>
    SearchResult,

    /// <summary>«Dictar» next to the search field: focuses it and starts Windows dictation (Win+H).</summary>
    SearchDictate,

    /// <summary>The search field itself: focuses it and shows the touch keyboard.</summary>
    SearchField,

    /// <summary>«Cerrar» on the search surface: gives the foreground back without a result.</summary>
    SearchClose,

    /// <summary>Guide strip: «Funcionó».</summary>
    GuideWorked,

    /// <summary>Guide strip: «Falló».</summary>
    GuideFailed,

    /// <summary>Guide strip: «Repetir».</summary>
    GuideRepeat,

    /// <summary>Guide strip: «Siguiente».</summary>
    GuideNext,

    /// <summary>Guide strip: the extra action of the current step.</summary>
    GuideStepAction,
}
