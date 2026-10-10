namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>
/// The addresses of «Acerca de y contacto» (ACE-001, ACE-005), in one place so they can be checked before publishing.
/// LinkedIn and the contact email are optional: while one is empty its button is hidden and nothing invented is ever
/// shown in its place (user decision D11). The email is also the only address [Enviar por correo] may open the email
/// app for (ACE-004, ADR-0029).
/// </summary>
/// <param name="Repository">The code repository: [GitHub], [shareShort] and «Colaborar con el código».</param>
/// <param name="Issues">«Reportar en GitHub».</param>
/// <param name="Guide">«Guía de usuario», read in the browser.</param>
/// <param name="LinkedIn">The LinkedIn profile, if there is a real one.</param>
/// <param name="Email">The contact email of the project, if it exists.</param>
public sealed record AboutLinks(Uri Repository, Uri Issues, Uri Guide, Uri? LinkedIn, string? Email)
{
    /// <summary>
    /// The addresses of this version: the public repository, its issues and the user guide. There is no real LinkedIn
    /// address nor contact email yet (the prototype's were placeholders, docs/05 §6, PQ-38 and D11).
    /// </summary>
    public static AboutLinks Current { get; } =
        new(
            new Uri("https://github.com/Michael-Coaguila/clicalo"),
            new Uri("https://github.com/Michael-Coaguila/clicalo/issues"),
            new Uri(
                "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/guia-de-usuario.md"
            ),
            null,
            null
        );
}
