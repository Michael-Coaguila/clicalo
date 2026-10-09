namespace Clicalo.Application.UseCases.Editor;

/// <summary>Where the position buttons of «Más opciones» move a shortcut (EDI-016). Pure.</summary>
public static class Positions
{
    /// <summary>
    /// The zero-based place <paramref name="move"/> leads to from <paramref name="index"/> in a list of
    /// <paramref name="count"/>, or <see langword="null"/> when the button does not apply (it would not move or it
    /// would leave the list): those buttons are shown disabled.
    /// </summary>
    /// <param name="move">The button.</param>
    /// <param name="index">The current zero-based place.</param>
    /// <param name="count">How many shortcuts the list has.</param>
    public static int? Target(PositionMove move, int index, int count)
    {
        if (index < 0 || index >= count)
        {
            return null;
        }

        var target = move switch
        {
            PositionMove.First => 0,
            PositionMove.Before => index - 1,
            PositionMove.After => index + 1,
            _ => count - 1,
        };
        return target < 0 || target >= count || target == index ? null : target;
    }
}
