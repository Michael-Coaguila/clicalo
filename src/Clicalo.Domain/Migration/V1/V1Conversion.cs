using Clicalo.Domain.Document;

namespace Clicalo.Domain.Migration.V1;

/// <summary>The converted document and its report.</summary>
/// <param name="Document">The document, valid and complete (never written if the conversion fails, MIG-004).</param>
/// <param name="Report">The report.</param>
public sealed record V1Conversion(UserDocument Document, MigrationReport Report);
