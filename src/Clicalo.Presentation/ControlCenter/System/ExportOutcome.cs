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

    /// <summary>
    /// The place chosen for the copy that comes before deleting the data is inside the data folders, which the
    /// uninstaller deletes: nothing was written (REG-08).
    /// </summary>
    InsideData,
}
