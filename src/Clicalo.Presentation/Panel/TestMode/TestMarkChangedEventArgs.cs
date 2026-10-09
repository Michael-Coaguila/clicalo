using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Presentation.Panel.TestMode;

/// <summary>A tile's test mode mark appeared or went away (TAC-008).</summary>
/// <param name="shortcut">The tile's shortcut.</param>
/// <param name="mark">The mark on show, or <see langword="null"/> when it went away.</param>
public sealed class TestMarkChangedEventArgs(ShortcutId shortcut, TestModeMark? mark) : EventArgs
{
    /// <summary>The tile's shortcut.</summary>
    public ShortcutId Shortcut { get; } = shortcut;

    /// <summary>The mark on show, or <see langword="null"/> when it went away.</summary>
    public TestModeMark? Mark { get; } = mark;
}
