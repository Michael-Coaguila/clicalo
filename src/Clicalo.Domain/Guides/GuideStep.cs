using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Guides;

/// <summary>One step of a guide (R-39): what it points at, what it says and what it waits for.</summary>
/// <param name="Target">The control it points at.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its text; a ★ in it is drawn as the star icon.</param>
/// <param name="Awaits">
/// What the person may do to go on by themselves; <see langword="null"/> for a step that only explains. [next] is always
/// there.
/// </param>
/// <param name="OnlyWithTarget">The step is left out while its control is not on screen (the pager with one page).</param>
public sealed record GuideStep(
    GuideTarget Target,
    Message Title,
    Message Text,
    GuideSignal? Awaits = null,
    bool OnlyWithTarget = false
);
