namespace Clicalo.Infrastructure.Persistence;

/// <summary>A shared profile ready to be written as <c>clicalo-perfil-&lt;id&gt;.json</c> (DAT-007).</summary>
/// <param name="FileName">The suggested file name.</param>
/// <param name="Content">The UTF-8 bytes.</param>
/// <param name="ExcludedTexts">Texts left out (the user is warned); 0 when they were included in clear.</param>
public sealed record ProfileShareExport(
    string FileName,
    ReadOnlyMemory<byte> Content,
    int ExcludedTexts
);
