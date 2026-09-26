namespace Clicalo.Domain.Migration.V1;

/// <summary>One line of the migration report shown by the welcome (MIG-004).</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Profile">The v1 profile name, if it applies.</param>
/// <param name="Button">The v1 button label, if it applies.</param>
/// <param name="Original">The original text (token, colour, address), if it applies.</param>
public sealed record MigrationNote(
    MigrationNoteKind Kind,
    string? Profile,
    string? Button,
    string? Original
);
