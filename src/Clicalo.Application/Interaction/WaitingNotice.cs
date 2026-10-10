namespace Clicalo.Application.Interaction;

/// <summary>A notice that waits in the <see cref="NoticeQueue"/> with the time it shows for once its turn comes.</summary>
/// <param name="Notice">The notice.</param>
/// <param name="Duration">How long it shows.</param>
public sealed record WaitingNotice(Notice Notice, TimeSpan Duration);
