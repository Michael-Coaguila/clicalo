using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The «Probar» card (PRB-001 to PRB-005).</summary>
/// <param name="Title">[testTitle].</param>
/// <param name="CloseName">[close].</param>
/// <param name="Sequence">What it will do.</param>
/// <param name="PlayText">[playSeq].</param>
/// <param name="What">The [tw*] sentence of the kind.</param>
/// <param name="PhaseIcon">The icon of the phase line.</param>
/// <param name="Phase">The phase line, once played.</param>
/// <param name="TargetsLabel">[testIn].</param>
/// <param name="Targets">The open apps; the chosen one marked.</param>
/// <param name="NoApps">The text when no app is open.</param>
/// <param name="LiveText">[testLive2].</param>
/// <param name="CanLive">Whether «Probar ahora» applies.</param>
/// <param name="How">[testHow].</param>
/// <param name="Question">[testAskQ] while it waits for an answer.</param>
/// <param name="YesText">[yesWorked].</param>
/// <param name="NoText">[noWorked].</param>
/// <param name="Answer">[testOk] after yes.</param>
/// <param name="TipsTitle">[tipsTitle] after no.</param>
/// <param name="Tips">[tip1] to [tip4] after no.</param>
/// <param name="Running">Whether a try is running.</param>
public sealed record TestModel(
    string Title,
    string CloseName,
    ValueList<SequenceItem> Sequence,
    string PlayText,
    string What,
    string? PhaseIcon,
    string? Phase,
    string TargetsLabel,
    ValueList<AppChip> Targets,
    string? NoApps,
    string LiveText,
    bool CanLive,
    string How,
    string? Question,
    string YesText,
    string NoText,
    string? Answer,
    string? TipsTitle,
    ValueList<string> Tips,
    bool Running
);
