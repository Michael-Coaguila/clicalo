using Clicalo.Application.Localization;

namespace Clicalo.App.Lifecycle;

/// <summary>What the start read from disk (<see cref="StartupReader"/>).</summary>
/// <param name="Documents">The document of this start and whether it still has to be written.</param>
/// <param name="Localization">The language files, in the language the document asks for.</param>
internal sealed record StartupRead(StartupLoad Documents, LocalizationContext Localization);
