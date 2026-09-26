namespace Clicalo.Domain.Commands;

/// <summary>The document was replaced by a backup (RestoreBackup): views are reconciled with it.</summary>
public sealed record DocumentRestored : DomainEvent;
