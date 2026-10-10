namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>An installed program of «Elegir programa» (EDI-014).</summary>
/// <param name="Target">What the App field gets when it is chosen.</param>
/// <param name="Name">Its name.</param>
/// <param name="SearchName">
/// Its name as the filter compares it (<see cref="ProgramFilter.Fold"/>): folded once when the list is read, not on
/// every keystroke.
/// </param>
public sealed record ProgramChip(string Target, string Name, string SearchName = "")
{
    /// <summary>The chip of <paramref name="name"/>, with its name folded for the filter.</summary>
    /// <param name="target">What the App field gets when it is chosen.</param>
    /// <param name="name">Its name.</param>
    public static ProgramChip Of(string target, string name) =>
        new(target, name, ProgramFilter.Fold(name));
}
