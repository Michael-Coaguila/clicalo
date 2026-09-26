using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>Why a CSS value was rejected, and where (0-based offset into the parsed text).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct CssSyntaxError
{
    public CssSyntaxError(int offset, string message)
    {
        Offset = offset;
        Message = message;
    }

    public int Offset { get; }

    public string Message { get; }
}
