namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The tone of a warning under the combination box (EDI-007).</summary>
public enum WarningTone
{
    /// <summary>No warning.</summary>
    None,

    /// <summary>Blocked: red with the block icon.</summary>
    Danger,

    /// <summary>Special: warn with the info icon.</summary>
    Warn,
}
