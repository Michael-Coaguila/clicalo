namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>What [Exportar] did.</summary>
public enum ExportOutcome
{
    /// <summary>The file was written.</summary>
    Done,

    /// <summary>The person closed the picker.</summary>
    Cancelled,

    /// <summary>The file could not be written.</summary>
    Failed,
}
