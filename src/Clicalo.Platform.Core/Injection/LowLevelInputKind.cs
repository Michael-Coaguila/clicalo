namespace Clicalo.Platform.Core.Injection;

/// <summary>What a <see cref="LowLevelInput"/> does.</summary>
public enum LowLevelInputKind
{
    /// <summary>Key down.</summary>
    KeyDown,

    /// <summary>Key up.</summary>
    KeyUp,

    /// <summary>A UTF-16 unit down and up (<c>KEYEVENTF_UNICODE</c>).</summary>
    Unicode,

    /// <summary>Absolute move on the virtual desktop (<c>MOVE | ABSOLUTE | VIRTUALDESK</c>).</summary>
    MouseMove,

    /// <summary>A mouse button down.</summary>
    MouseButtonDown,

    /// <summary>A mouse button up.</summary>
    MouseButtonUp,

    /// <summary>A vertical wheel step.</summary>
    Wheel,

    /// <summary>A horizontal wheel step.</summary>
    HorizontalWheel,
}
