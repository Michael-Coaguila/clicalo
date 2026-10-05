using Clicalo.Domain.Library;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>A shared profile read for the preview of Templates (DAT-007): nothing is installed or executed yet (LOG-006).</summary>
/// <param name="Profile">The profile with new ids for itself and every shortcut (DAT-004).</param>
/// <param name="UnavailableTexts">Texts left out or encrypted elsewhere: the shortcuts are incomplete (COP-005).</param>
/// <param name="RiskyShortcuts">Web, App and Macro shortcuts, shown and confirmed one by one before installing (LOG-008).</param>
public sealed record ProfileShareImport(Profile Profile, int UnavailableTexts, int RiskyShortcuts);
