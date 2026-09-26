namespace Clicalo.Domain.Migration.V1;

/// <summary>Counts compared before and after the import (MIG-004: equal, separators, addresses and apps included).</summary>
/// <param name="Profiles">Profiles.</param>
/// <param name="Buttons">Buttons, separators included.</param>
/// <param name="Separators">Separators.</param>
/// <param name="Urls">Web buttons.</param>
/// <param name="Apps">App buttons.</param>
public sealed record V1Counts(int Profiles, int Buttons, int Separators, int Urls, int Apps);
