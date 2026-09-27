namespace Clicalo.Performance;

/// <summary>One start of <c>Clicalo.exe</c> measured by spike S5.</summary>
/// <param name="Variant">The publication variant (<c>sc-r2r</c>, <c>sc-r2r-composite</c>, <c>fdd</c>…).</param>
/// <param name="Run">1 for the first start after publishing (the closest to a cold start a hosted runner offers), then 2, 3…</param>
/// <param name="FirstFrame">From the creation of the process to the first frame of the panel.</param>
/// <param name="WorkingSetBytes">Working set of Clicalo.exe once the start settled.</param>
/// <param name="PrivateBytes">Private bytes of Clicalo.exe once the start settled.</param>
internal sealed record StartupSample(
    string Variant,
    int Run,
    TimeSpan FirstFrame,
    long WorkingSetBytes,
    long PrivateBytes
);
