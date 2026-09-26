using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>The live measurements the guide strip shows next to the current step.</summary>
internal sealed record LabStatus
{
    /// <summary>The window in front, described («notepad», «SpikeLab · Panel#0»).</summary>
    public string Foreground { get; init; } = "desconocido";

    /// <summary>What the counters did since the current step started («Empezar ciclo», «Repetir» or «Siguiente»).</summary>
    public MeasurementCounters SinceStep { get; init; }

    /// <summary><c>reg01.violations</c> since the process started.</summary>
    public long TotalViolations { get; init; }

    /// <summary>«Última orden».</summary>
    public string? LastCommand { get; init; }

    /// <summary>Latency of the last tap or command, in milliseconds.</summary>
    public double? LastLatencyMs { get; init; }

    /// <summary>«Concesión».</summary>
    public string? LastLease { get; init; }

    /// <summary>«Devolución».</summary>
    public string? LastRestore { get; init; }

    /// <summary>True while InputProbe is open.</summary>
    public bool ProbeOpen { get; init; }

    /// <summary>The last notice for the maintainer.</summary>
    public string? Notice { get; init; }

    /// <summary>Pieces of the product that are not ready yet.</summary>
    public int PiecesNotReady { get; init; }

    /// <summary>True while «Enviar teclas» is on.</summary>
    public bool SendsKeys { get; init; }

    /// <summary>True while «Números de voz» is on (it names the step action of S3 row 6).</summary>
    public bool VoiceNumbers { get; init; }
}
