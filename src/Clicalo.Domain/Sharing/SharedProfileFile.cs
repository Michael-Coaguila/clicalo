namespace Clicalo.Domain.Sharing;

/// <summary>A profile ready to share as a file (DAT-007).</summary>
/// <param name="FileName">The suggested name, <c>clicalo-perfil-&lt;id&gt;.json</c>.</param>
/// <param name="Content">The UTF-8 bytes.</param>
/// <param name="ExcludedTexts">Texts left out (the person is told); 0 when they were included in clear.</param>
public sealed record SharedProfileFile(
    string FileName,
    ReadOnlyMemory<byte> Content,
    int ExcludedTexts
);
