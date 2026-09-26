namespace Clicalo.TestKit.Windows.Input;

/// <summary>The frames a <see cref="SyntheticPointer"/> contact gesture is made of.</summary>
internal enum ContactFrame
{
    /// <summary>A pen comes into range above the point without touching.</summary>
    Hover,

    /// <summary>The contact goes down.</summary>
    Down,

    /// <summary>The contact stays down, maybe at another point.</summary>
    Update,

    /// <summary>The contact goes up normally.</summary>
    Up,

    /// <summary>The contact goes up cancelled (a check failed while it was down).</summary>
    Cancel,

    /// <summary>A lifted pen leaves the detection range.</summary>
    Leave,
}
