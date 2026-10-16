namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// Who owns an icon picker (D14): the editor of a shortcut, the card of a profile or «Perfil vacío». The view of the
/// picker sends every tap here, so the three draw the same layer.
/// </summary>
public interface IIconPicker
{
    /// <summary>An icon of the grid or a suggestion.</summary>
    /// <param name="icon">The Material Symbols name.</param>
    void PickIcon(string icon);

    /// <summary>The search, as typed or dictated.</summary>
    /// <param name="query">What the person typed.</param>
    void SearchIcons(string query);

    /// <summary>A category of the picker.</summary>
    /// <param name="category">The category, or null for «Todos».</param>
    void ChooseIconCategory(string? category);

    /// <summary>✕ of the picker.</summary>
    void CloseIconPicker();

    /// <summary>🎤 of the search: dictation into the field that has the keyboard (ACC-011).</summary>
    void DictateIconSearch();
}
