using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases;

/// <summary>
/// The document of a first installation from the starter kit (user decision D2 of 2026-10-03; BIE-003, BIE-006,
/// BIE-009, CAT-003). Pure: the content comes already loaded and validated, and nothing is written here.
/// <list type="bullet">
/// <item><see cref="Create"/>: what the welcome step «Which apps do you use most?» marked, in the programs language of the
/// settings (PLA-009). Nothing marked starts empty, with General and Always visible only.</item>
/// <item><see cref="CreateDefault"/>: the options marked by default (only «Basics»), which is what «Skip» applies and
/// what a first start installs until the welcome exists (M4).</item>
/// </list>
/// </summary>
public static class FirstDocument
{
    /// <summary>The new document of <paramref name="selection"/>.</summary>
    /// <param name="content">The kit, the seed and the templates.</param>
    /// <param name="selection">The marked options.</param>
    /// <param name="settings">The settings of the new document; its programs language picks the variants.</param>
    /// <param name="ids">The source of new ids (DAT-004).</param>
    public static Result<UserDocument> Create(
        StarterContent content,
        StarterSelection selection,
        UserSettings settings,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ids);
        return StarterLibrary
            .Build(content, selection, settings.Keyboard.AppsLanguage, ids)
            .Map(library => UserDocument.Create(library, settings));
    }

    /// <summary>The new document of the options marked by default, the same that «Skip» applies (BIE-003).</summary>
    /// <param name="content">The kit, the seed and the templates.</param>
    /// <param name="settings">The settings of the new document.</param>
    /// <param name="ids">The source of new ids (DAT-004).</param>
    public static Result<UserDocument> CreateDefault(
        StarterContent content,
        UserSettings settings,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        return Create(content, content.Kit.DefaultSelection, settings, ids);
    }
}
