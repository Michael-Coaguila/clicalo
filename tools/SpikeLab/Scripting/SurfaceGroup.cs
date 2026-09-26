namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>The surfaces under test of S1, as the rows of its results table name them.</summary>
internal enum SurfaceGroup
{
    /// <summary>Any surface under test (the guide strip never counts).</summary>
    Any,

    /// <summary>The panel.</summary>
    Panel,

    /// <summary>The edge bar («Pestaña») with its handle and its side window.</summary>
    TabWithSide,

    /// <summary>The 64 px bubble.</summary>
    Bubble,
}
