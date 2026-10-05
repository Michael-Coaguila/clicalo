namespace Clicalo.Domain.Catalog;

/// <summary>
/// A system action that cannot be sent as keys (EJE-016): lock the computer, brightness up or down. From
/// <c>data/catalogs/system-commands.json</c>.
/// </summary>
/// <param name="Value">The command identifier, exactly as persisted.</param>
public readonly record struct SystemCommandId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
