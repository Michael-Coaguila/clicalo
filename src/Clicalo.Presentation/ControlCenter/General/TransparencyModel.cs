namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Transparencia» (GEN-009).</summary>
/// <param name="Caption">[secTransp].</param>
/// <param name="OpacityTitle">[opacity].</param>
/// <param name="OpacityPercent">The opacity, 30 to 100.</param>
/// <param name="OpacityValue">«92%».</param>
/// <param name="OpacityLessName">[opLess].</param>
/// <param name="OpacityMoreName">[opMore].</param>
/// <param name="Behind">[behind], the «document» under the preview.</param>
/// <param name="AutoDim">[autoDim].</param>
/// <param name="DimTitle">[dimLevel].</param>
/// <param name="DimPercent">The dimmed opacity, 10 to 80.</param>
/// <param name="DimValue">«35%».</param>
/// <param name="DimLessName">The accessible name of − of the dimmed opacity.</param>
/// <param name="DimMoreName">The accessible name of + of the dimmed opacity.</param>
public sealed record TransparencyModel(
    string Caption,
    string OpacityTitle,
    int OpacityPercent,
    string OpacityValue,
    string OpacityLessName,
    string OpacityMoreName,
    string Behind,
    SwitchItem AutoDim,
    string DimTitle,
    int DimPercent,
    string DimValue,
    string DimLessName,
    string DimMoreName
);
