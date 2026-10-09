namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>Everything «Atajos» shows outside the editor, projected at once.</summary>
/// <param name="TopBar">The top bar.</param>
/// <param name="Profiles">The profiles column.</param>
/// <param name="Header">The header of the list.</param>
/// <param name="ProfileEdit">The profile card, when open.</param>
/// <param name="Link">The binding row; null in Always visible.</param>
/// <param name="Grid">The grid.</param>
/// <param name="Column">What the editor column shows.</param>
/// <param name="EmptyText">[pickOne].</param>
/// <param name="Library">«Añadir atajo», when open.</param>
public sealed record ShortcutsScreen(
    TopBarModel TopBar,
    ProfilesModel Profiles,
    ListHeaderModel Header,
    ProfileEditModel? ProfileEdit,
    LinkModel? Link,
    GridModel Grid,
    EditorColumn Column,
    string EmptyText,
    LibraryModel? Library
);
