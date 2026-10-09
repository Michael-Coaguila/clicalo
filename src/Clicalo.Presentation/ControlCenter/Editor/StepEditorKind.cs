namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>What an open macro step edits (EDI-013).</summary>
public enum StepEditorKind
{
    /// <summary>The step is closed.</summary>
    None,

    /// <summary>Its keys, in the combination box.</summary>
    Keys,

    /// <summary>Its duration, with − and +.</summary>
    Wait,

    /// <summary>Its text.</summary>
    Text,

    /// <summary>Its mouse action, with chips.</summary>
    Mouse,
}
