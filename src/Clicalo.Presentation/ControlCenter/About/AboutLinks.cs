namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>
/// The addresses of «Acerca de y contacto» (ACE-001, ACE-005), in one place so they can be checked before publishing.
/// LinkedIn and the contact email are optional: without a real address the LinkedIn button is not shown and the email
/// row shows the visible marker [contactPending] instead of an invented address.
/// </summary>
/// <param name="Repository">The code repository: [GitHub], [shareShort] and «Colaborar con el código».</param>
/// <param name="Issues">«Reportar en GitHub».</param>
/// <param name="LinkedIn">The LinkedIn profile, if there is a real one.</param>
/// <param name="Email">The contact email, if it exists.</param>
public sealed record AboutLinks(Uri Repository, Uri Issues, Uri? LinkedIn, string? Email)
{
    /// <summary>
    /// The addresses of this version: the public repository and its issues. There is no real LinkedIn address nor
    /// contact email yet (the prototype's were placeholders, docs/05 §6 and PQ-38).
    /// </summary>
    public static AboutLinks Current { get; } =
        new(
            new Uri("https://github.com/Michael-Coaguila/clicalo"),
            new Uri("https://github.com/Michael-Coaguila/clicalo/issues"),
            null,
            null
        );
}
