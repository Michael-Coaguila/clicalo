using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Editor;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>«Perfil vacío» (PLA-010).</summary>
/// <param name="Title">[blankTitle].</param>
/// <param name="Subtitle">[blankSub].</param>
/// <param name="Open">Whether it is unfolded.</param>
/// <param name="Icon">The icon of the profile.</param>
/// <param name="ChangeIconName">[changeIcon].</param>
/// <param name="IconsOpen">Whether the icon picker shows.</param>
/// <param name="Picker">The icon picker (D14), when open: the whole collection, the 28 of a profile first.</param>
/// <param name="Placeholder">[blankPh].</param>
/// <param name="Name">The name typed.</param>
/// <param name="DictateName">[dictName].</param>
/// <param name="LinkTitle">[blankLink].</param>
/// <param name="Links">The options of «Se activa con».</param>
/// <param name="CreateText">[blankCreate].</param>
/// <param name="CanCreate">Whether there is a name.</param>
public sealed record BlankModel(
    string Title,
    string Subtitle,
    bool Open,
    string Icon,
    string ChangeIconName,
    bool IconsOpen,
    IconPickerModel? Picker,
    string Placeholder,
    string Name,
    string DictateName,
    string LinkTitle,
    ValueList<BlankLink> Links,
    string CreateText,
    bool CanCreate
);
