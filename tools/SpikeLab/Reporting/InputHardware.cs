using System.Globalization;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// The input hardware Windows reports (<c>GetSystemMetrics</c>: <c>SM_DIGITIZER</c>, <c>SM_MAXIMUMTOUCHES</c> and
/// <c>SM_MOUSEPRESENT</c>), so a row left «no aplicable» for lack of a pen or a mouse can be checked in the report.
/// </summary>
/// <param name="IntegratedTouch">A touch screen built into the machine.</param>
/// <param name="ExternalTouch">An external touch device.</param>
/// <param name="IntegratedPen">A pen digitizer built into the machine.</param>
/// <param name="ExternalPen">An external pen digitizer.</param>
/// <param name="Ready">The digitizer is ready for input.</param>
/// <param name="MaxTouches">Simultaneous contacts the digitizer supports; 0 without touch.</param>
/// <param name="MousePresent">Windows says a mouse is present (a touchpad counts).</param>
internal sealed record InputHardware(
    bool IntegratedTouch,
    bool ExternalTouch,
    bool IntegratedPen,
    bool ExternalPen,
    bool Ready,
    int MaxTouches,
    bool MousePresent
)
{
    /// <summary>Nothing known (the report of a machine that was not asked).</summary>
    public static InputHardware Unknown { get; } = new(false, false, false, false, false, 0, false);

    /// <summary>True with any touch digitizer.</summary>
    public bool HasTouch => IntegratedTouch || ExternalTouch;

    /// <summary>True with any pen digitizer.</summary>
    public bool HasPen => IntegratedPen || ExternalPen;

    /// <summary>«Pantalla táctil: sí (integrada, 10 contactos) · Lápiz: sí · Mouse: sí».</summary>
    public string Describe()
    {
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        var touch = HasTouch
            ? string.Create(
                spanish,
                $"sí ({(IntegratedTouch ? "integrada" : "externa")}, {MaxTouches} contactos{(Ready ? string.Empty : ", no lista")})"
            )
            : "no";
        return "Pantalla táctil: "
            + touch
            + " · Lápiz: "
            + (HasPen ? "sí" : "no")
            + " · Mouse: "
            + (MousePresent ? "sí" : "no");
    }
}
