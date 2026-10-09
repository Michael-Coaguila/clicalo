namespace Clicalo.Application.UseCases.Editor;

/// <summary>The four position buttons of «Más opciones» (EDI-016).</summary>
public enum PositionMove
{
    /// <summary>[first]: to the start.</summary>
    First,

    /// <summary>[before]: one place earlier.</summary>
    Before,

    /// <summary>[after]: one place later.</summary>
    After,

    /// <summary>[lastPos]: to the end.</summary>
    Last,
}
