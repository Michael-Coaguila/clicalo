using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Library;

/// <summary>One section of <c>data/content/library.json</c> (ATJ-010): Edición, Ventanas, Mouse, Voz, Textos or Sistema.</summary>
/// <param name="Id">Its id in the file (<c>edit</c>, <c>win</c>, <c>mouse</c>, <c>voice</c>, <c>text</c>, <c>sys</c>).</param>
/// <param name="Shortcuts">Its ready actions, in order.</param>
public sealed record LibrarySection(string Id, ValueList<TemplateShortcut> Shortcuts);
