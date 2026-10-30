namespace Clicalo.Application.Interaction;

/// <summary>
/// How the <see cref="NoticeQueue"/> treats a notice (AVI-002), from the lowest priority to the highest: a waiting
/// safety notice shows before a waiting notice with [undo].
/// </summary>
public enum NoticeKind
{
    /// <summary>A plain notice: the next one replaces it.</summary>
    Normal = 0,

    /// <summary>
    /// A notice that offers [undo] (AVI-003, AVI-005): it is never lost; when another notice arrives it waits and shows
    /// again afterwards. Only the newest one offers [undo], since that is the operation the button would undo.
    /// </summary>
    Undo = 1,

    /// <summary>
    /// A safety notice ([releasedAuto], [releasedSwitch], «not sent: elevated app»): it is never lost; when another
    /// notice arrives it waits and shows again afterwards.
    /// </summary>
    Safety = 2,
}
