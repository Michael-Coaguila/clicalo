namespace Clicalo.Domain.Touch;

/// <summary>
/// Who produced the input, from <c>GetCurrentInputMessageSource</c> while the pointer message is processed
/// (blueprint §3.3 rule 4, §8.3). Spike S13 (M7) decides whether the distinction is reliable enough for uiAccess.
/// </summary>
public enum PointerInputOrigin
{
    /// <summary>The source could not be read or is not specific (<c>IMO_UNAVAILABLE</c>).</summary>
    Unknown,

    /// <summary>A physical device (<c>IMO_HARDWARE</c>).</summary>
    Hardware,

    /// <summary>Injected by a program (<c>IMO_INJECTED</c>): <c>InjectTouchInput</c>, synthetic pointers, tests.</summary>
    Injected,

    /// <summary>Produced by the system (<c>IMO_SYSTEM</c>).</summary>
    System,
}
