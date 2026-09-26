using Clicalo.Domain.Catalog;
using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Measurement;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>What every laboratory surface shares.</summary>
/// <param name="Time">Stamps pointer frames and schedules gesture deadlines.</param>
/// <param name="Sink">Receives taps, commands, drags and activation messages.</param>
/// <param name="Board">Records whether the pointer layer and the windowing work.</param>
/// <param name="Directory">Where each surface registers its window.</param>
/// <param name="Log">The timeline (DPI changes).</param>
internal sealed record LabSurfaceContext(
    TimeProvider Time,
    ILabInputSink Sink,
    ComponentBoard Board,
    SurfaceDirectory Directory,
    LabEventLog Log
)
{
    /// <summary>
    /// The filter values of the default touch preset (TAC-001), converted here because Domain.Touch cannot depend on
    /// Domain.Catalog (M1-ownership.md, pointer package).
    /// </summary>
    public static TouchSettings DefaultTouchSettings
    {
        get
        {
            var preset = TouchPresets.Default;
            return new TouchSettings(
                preset.Debounce,
                preset.HitSlopPx,
                preset.CancelMovePx,
                preset.MinContact
            );
        }
    }
}
