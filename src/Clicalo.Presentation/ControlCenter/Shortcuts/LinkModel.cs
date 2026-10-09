using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The binding row of the profile in view (ATJ-005 to ATJ-008).</summary>
/// <param name="State">Its state.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Title">Its title.</param>
/// <param name="Subtitle">Its explanation.</param>
/// <param name="Action">[change], [linkBtn] or [cancel]; null for General.</param>
/// <param name="Expanded">Whether the options show.</param>
/// <param name="AppsTitle">[linkOpenApps].</param>
/// <param name="Apps">The open apps, the bound ones marked.</param>
/// <param name="DetectText">[linkDetect].</param>
/// <param name="NoneText">[linkNone].</param>
/// <param name="Question">[processTaken] while it waits for an answer.</param>
/// <param name="YesText">The answer that moves the process.</param>
/// <param name="NoText">[cancel].</param>
public sealed record LinkModel(
    LinkState State,
    string Icon,
    string Title,
    string Subtitle,
    string? Action,
    bool Expanded,
    string AppsTitle,
    ValueList<AppChip> Apps,
    string DetectText,
    string NoneText,
    string? Question,
    string YesText,
    string NoText
);
