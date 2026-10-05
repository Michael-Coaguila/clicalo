using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>What a command may know about the world (blueprint §6.3), passed as values.</summary>
/// <param name="Now">The current time, from <see cref="TimeProvider"/>.</param>
/// <param name="Ids">Source of new ids.</param>
/// <param name="UiLanguage">Interface language.</param>
/// <param name="AppsLanguage">Language of the target apps.</param>
public sealed record DomainContext(
    DateTimeOffset Now,
    IIdGenerator Ids,
    LangCode UiLanguage,
    LangCode AppsLanguage
);
