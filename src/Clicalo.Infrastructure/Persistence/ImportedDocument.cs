using System.Collections.Immutable;
using Clicalo.Domain.Document;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// A document chosen to import (a backup or an exported copy), validated and summarized before the user picks Merge or
/// Replace (COP-002): its counts, version and warnings.
/// </summary>
/// <param name="Document">The valid document; nothing in it has been executed or installed (LOG-006).</param>
/// <param name="Schema">Its schema version.</param>
/// <param name="WrittenBy">The app version that wrote it.</param>
/// <param name="WrittenAt">When it was written.</param>
/// <param name="Profiles">Profiles in it, General included.</param>
/// <param name="Shortcuts">Shortcuts in it, Always visible included.</param>
/// <param name="UnavailableTexts">Texts encrypted for another user or machine: imported as unavailable (COP-005).</param>
/// <param name="Repairs">Stable codes of what was repaired to make it valid.</param>
public sealed record ImportedDocument(
    UserDocument Document,
    SchemaVersion Schema,
    string WrittenBy,
    DateTimeOffset WrittenAt,
    int Profiles,
    int Shortcuts,
    int UnavailableTexts,
    ImmutableArray<string> Repairs
);
